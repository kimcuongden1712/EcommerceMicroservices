using Contracts.Domains.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySql.EntityFrameworkCore.Extensions;
using Product.API.Entities;
using System.Text.Json;

namespace Product.API.Persistence;

public class ProductContext (DbContextOptions<ProductContext> options) : DbContext(options)
{
    private const string BinaryCollation = "utf8mb4_bin";

    public DbSet<Categories> Categories => Set<Categories>();
    public DbSet<Products> Products => Set<Products>();
    public DbSet<ProductVariants> ProductVariants => Set<ProductVariants>();
    public DbSet<ProductImages> ProductImages => Set<ProductImages>();
    public DbSet<ProductImageUploads> ProductImageUploads => Set<ProductImageUploads>();
    public DbSet<CatalogAudit> CatalogAudit => Set<CatalogAudit>();
    public DbSet<CatalogOutbox> CatalogOutbox => Set<CatalogOutbox>();

    protected override void OnModelCreating (ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var categories = modelBuilder.Entity<Categories>();
        categories.ToTable("categories", table =>
            table.HasCheckConstraint("ck_categories_status", "status IN (0, 1)")
        );
        categories.Property(x => x.Name).HasMaxLength(120);
        ConfigureBinaryKey(categories.Property(x => x.Slug), 120);
        categories.HasIndex(x => x.Slug).IsUnique();
        categories.HasIndex(x => new { x.ParentId, x.Status, x.SortOrder, x.Id });
        categories.HasOne<Categories>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        categories.Property(x => x.Version).IsConcurrencyToken();
        ConfigureAuditDates(categories);

        var products = modelBuilder.Entity<Products>();
        products.ToTable("products", table =>
            {
                table.HasCheckConstraint("ck_products_description_length", "CHAR_LENGTH(description) <= 10000");
                table.HasCheckConstraint("ck_products_status", "status IN (0, 1, 2)");
            }
        );
        products.Property(x => x.Name).HasMaxLength(160);
        ConfigureBinaryKey(products.Property(x => x.Slug), 160);
        products.Property(x => x.Description).HasMaxLength(10000).HasColumnType("text");
        products.HasIndex(x => x.Slug).IsUnique();
        products.HasIndex(x => new { x.CategoryId, x.Status, x.PublishedAt, x.Id });
        products.HasIndex(x => new { x.Status, x.PublishedAt, x.Id });
        products.HasOne<Categories>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        products.Property(x => x.Version).IsConcurrencyToken();
        ConfigureAuditDates(products);

        var variants = modelBuilder.Entity<ProductVariants>();
        variants.ToTable("product_variants", table =>
        {
            table.HasCheckConstraint("ck_product_variants_price", "price_minor BETWEEN 1 AND 99999999");
            table.HasCheckConstraint("ck_product_variants_dimensions",
                "weight_g > 0 AND length_mm > 0 AND width_mm > 0 AND height_mm > 0");
            table.HasCheckConstraint("ck_product_variants_status", "status IN (0, 1, 2)");
        });
        ConfigureBinaryKey(variants.Property(x => x.Sku), 64)
            .HasConversion(value => value.Trim().ToUpperInvariant(), value => value);
        ConfigureBinaryKey(variants.Property(x => x.ColorCode), 48)
            .HasConversion(value => value.Trim().ToUpperInvariant(), value => value);
        ConfigureBinaryKey(variants.Property(x => x.SizeCode), 32)
            .HasConversion(value => value.Trim().ToUpperInvariant(), value => value);
        variants.Property(x => x.ColorLabel).HasMaxLength(80);
        variants.Property(x => x.SizeLabel).HasMaxLength(80);
        variants.HasIndex(x => x.Sku).IsUnique();
        variants.HasIndex(x => new { x.ProductId, x.ColorCode, x.SizeCode }).IsUnique();
        variants.HasIndex(x => new { x.ProductId, x.Status, x.PriceMinor, x.Id });
        variants.HasOne<Products>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        var images = modelBuilder.Entity<ProductImages>();
        images.ToTable("product_images");
        ConfigureBinaryKey(images.Property(x => x.AssetKey), 255);
        images.Property(x => x.AltText).HasMaxLength(160);
        images.HasIndex(x => new { x.ProductId, x.Position }).IsUnique();
        images.HasIndex(x => x.AssetKey).IsUnique();
        images.HasOne<Products>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        var uploads = modelBuilder.Entity<ProductImageUploads>();
        uploads.ToTable("product_image_uploads", table =>
            table.HasCheckConstraint("ck_product_image_uploads_status", "status IN (0, 1, 2, 3, 4)")
        );
        ConfigureBinaryKey(uploads.Property(x => x.StagingKey), 255);
        ConfigureBinaryKey(uploads.Property(x => x.AssetKey), 255);
        uploads.Property(x => x.ExpectedContentType).HasMaxLength(100);
        uploads.HasIndex(x => x.StagingKey).IsUnique();
        uploads.HasIndex(x => new { x.Status, x.ExpiresAt });
        uploads.HasOne<Products>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        var audit = modelBuilder.Entity<CatalogAudit>();
        audit.ToTable("catalog_audit");
        audit.Property(x => x.Action).HasMaxLength(64);
        audit.Property(x => x.ActorId).HasMaxLength(128);
        audit.Property(x => x.CorrelationId).HasMaxLength(128);
        audit.Property(x => x.ChangedFieldsJson).HasColumnType("json");
        audit.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAt, x.Id });

        var outbox = modelBuilder.Entity<CatalogOutbox>();
        outbox.ToTable("catalog_outbox");
        outbox.Property(x => x.Topic).HasMaxLength(128);
        outbox.Property(x => x.PayloadJson).HasColumnType("json");
        outbox.HasIndex(x => new { x.PublishedAt, x.NextAttemptAt, x.Id });

        foreach (var entity in modelBuilder.Model.GetEntityTypes()) {
            foreach (var property in entity.GetProperties()) {
                if ((Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(DateTimeOffset))
                    property.SetColumnType("datetime(6)");

                if (property.Name is not (nameof(IDateTracking.CreatedDate) or nameof(IDateTracking.LastModifiedDate)))
                    property.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
            }
        }
    }

    public override int SaveChanges (bool acceptAllChangesOnSuccess)
    {
        UpdateAuditDates();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync (bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateAuditDates();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static PropertyBuilder<T> ConfigureBinaryKey<T> (PropertyBuilder<T> property, int maxLength)
    {
        property.HasMaxLength(maxLength);
        property.ForMySQLHasCharset("utf8mb4");
        property.ForMySQLHasCollation(BinaryCollation);
        return property;
    }

    private static void ConfigureAuditDates<TEntity> (EntityTypeBuilder<TEntity> entity)
        where TEntity : class, IDateTracking
    {
        entity.Property(x => x.CreatedDate).HasColumnName("created_at");
        entity.Property(x => x.LastModifiedDate).HasColumnName("updated_at");
    }

    private void UpdateAuditDates ()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IDateTracking>()) {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedDate = now;
            else if (entry.State == EntityState.Modified)
                entry.Entity.LastModifiedDate = now;
        }
    }
}
