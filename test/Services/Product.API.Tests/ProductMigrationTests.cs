using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Product.API.Persistence;

namespace Product.API.Tests;

public sealed class ProductMigrationTests
{
    [Fact]
    public void Every_migration_builds_a_target_model_and_reversible_mysql_operations()
    {
        using var context = new ProductContext(new DbContextOptionsBuilder<ProductContext>().UseMySQL("Server=localhost;Database=script_only").Options);
        var assembly = context.GetService<IMigrationsAssembly>();
        Assert.NotEmpty(assembly.Migrations);
        foreach (var pair in assembly.Migrations) {
            var migration = assembly.CreateMigration(pair.Value, context.Database.ProviderName!);
            Assert.NotEmpty(migration.UpOperations);
            Assert.NotEmpty(migration.DownOperations);
            Assert.NotEmpty(migration.TargetModel.GetEntityTypes());
        }
        var snapshot = assembly.ModelSnapshot!.Model;
        Assert.Equal(7, snapshot.GetEntityTypes().Count());
        var migrator = context.GetService<IMigrator>();
        var up = migrator.GenerateScript();
        var down = migrator.GenerateScript(assembly.Migrations.Keys.Last(), Migration.InitialDatabase);
        Assert.Contains("CREATE TABLE", up, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("product_image_uploads", up);
        Assert.Contains("DROP TABLE", down, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ProcessingStartedAt", string.Join(',', snapshot.GetEntityTypes().SelectMany(e => e.GetProperties()).Select(p => p.Name)));
    }
}
