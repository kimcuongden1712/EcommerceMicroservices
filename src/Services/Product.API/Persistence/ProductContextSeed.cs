using ILogger = Serilog.ILogger;

namespace Product.API.Persistence;

public class ProductContextSeed
{
    public static async Task SeedProductAsync (ProductContext productContext, ILogger logger)
    {
        if (!productContext.Products.Any()) {

            await productContext.SaveChangesAsync();
            logger.Information("Seeded data for Product DB associated with context {DbContextName}",
                nameof(ProductContext));
        }
    }
}