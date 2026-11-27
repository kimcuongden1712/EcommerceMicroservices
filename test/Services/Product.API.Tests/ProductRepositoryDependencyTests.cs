using Contracts.Domains.Interfaces;
using Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Product.API.Persistence;
using Product.API.Repositories;
using Product.API.Repositories.Interfaces;

namespace Product.API.Tests;

public sealed class ProductRepositoryDependencyTests
{
    [Fact]
    public void Product_and_image_repositories_resolve_without_circular_dependencies ()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ProductContext>();
        services.AddScoped(typeof(IRepositoryBase<,,>), typeof(RepositoryBase<,,>));
        services.AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductVariantsRepository, ProductVariantsRepository>();
        services.AddScoped<IProductImageUploadRepository, ProductImageUploadRepository>();
        services.AddScoped<IProductImageRepository, ProductImageRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        Assert.IsType<ProductRepository>(scope.ServiceProvider.GetRequiredService<IProductRepository>());
        Assert.IsType<ProductImageRepository>(scope.ServiceProvider.GetRequiredService<IProductImageRepository>());
    }
}
