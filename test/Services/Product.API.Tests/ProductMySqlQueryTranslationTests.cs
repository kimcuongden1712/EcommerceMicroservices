using System.Data.Common;
using Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Product.API.Entities;
using Product.API.Persistence;
using Product.API.Repositories;

namespace Product.API.Tests;

public sealed class ProductMySqlQueryTranslationTests
{
    private sealed class TranslationCompletedException : Exception { }
    private sealed class StopBeforeConnecting : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default) => throw new TranslationCompletedException();
    }

    [Fact]
    public async Task Ready_primary_image_query_translates_with_mysql_provider_before_opening_connection()
    {
        // This checks SQL translation, not MySQL execution. The interceptor prevents any network access.
        using var context=new ProductContext(new DbContextOptionsBuilder<ProductContext>()
            .UseMySQL("Server=localhost;Database=translation_only").AddInterceptors(new StopBeforeConnecting()).Options);
        var unit=new UnitOfWork<ProductContext>(context);
        var uploads=new ProductImageUploadRepository(context,unit,new RepositoryBase<ProductImages,long,ProductContext>(context,unit));
        var images=new ProductImageRepository(context,unit,uploads,new RepositoryBase<Products,long,ProductContext>(context,unit),new CategoryRepository(context,unit));
        await Assert.ThrowsAsync<TranslationCompletedException>(()=>images.HasReadyPrimaryImageAsync(1,TestContext.Current.CancellationToken));
    }
}
