using Microsoft.EntityFrameworkCore;
using Product.API.Entities;
using Product.API.Persistence;

namespace Product.API.Tests;

public sealed class ProductUploadLeaseMappingTests
{
    [Fact]
    public void Lease_mapping_preserves_microseconds_and_normalizes_to_utc()
    {
        using var context = new ProductContext(new DbContextOptionsBuilder<ProductContext>()
            .UseMySQL("Server=localhost;Database=mapping_only")
            .Options);
        var property = context.Model.FindEntityType(typeof(ProductImageUploads))!
            .FindProperty(nameof(ProductImageUploads.ProcessingStartedAt))!;
        var converter = property.GetValueConverter()!;
        var lease = new DateTimeOffset(2026, 11, 25, 22, 53, 4, TimeSpan.FromHours(7))
            .AddTicks(4185650);

        var stored = Assert.IsType<DateTime>(converter.ConvertToProvider(lease));
        var restored = Assert.IsType<DateTimeOffset>(converter.ConvertFromProvider(stored));

        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(lease.UtcDateTime, stored);
        Assert.Equal(lease, restored);
        Assert.Equal(TimeSpan.Zero, restored.Offset);
        Assert.Null(converter.ConvertToProvider(null));
        Assert.Null(converter.ConvertFromProvider(null));
    }
}
