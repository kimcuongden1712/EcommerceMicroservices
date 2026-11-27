using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Product.API.Extensions;
using Product.API.Persistence;

namespace Product.API.Tests;

public sealed class ProductHostExtensionsTests
{
    // Checks host orchestration; migration SQL is checked separately by ProductMigrationTests.
    private sealed class NoOpMigrator:IMigrator
    {
        public void Migrate(string? targetMigration=null) { }
        public Task MigrateAsync(string? targetMigration=null,CancellationToken cancellationToken=default)=>Task.CompletedTask;
        public string GenerateScript(string? fromMigration=null,string? toMigration=null,MigrationsSqlGenerationOptions options=MigrationsSqlGenerationOptions.Default)=>"";
        public bool HasPendingModelChanges()=>false;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Migration_helper_invokes_seeder_and_preserves_seeder_failure(bool fails)
    {
        using var host=Host.CreateDefaultBuilder().ConfigureServices(s=>{
            s.AddDbContext<ProductContext>(o=>o.UseMySQL("Server=localhost;Database=host_only").ReplaceService<IMigrator,NoOpMigrator>());
        }).Build();
        var invoked=false;var failure=new IOException("Seeder failed");var token=TestContext.Current.CancellationToken;
        Task Seed(ProductContext context,IServiceProvider services,CancellationToken ct){
            invoked=true;Assert.Same(context,services.GetRequiredService<ProductContext>());Assert.Equal(token,ct);
            return fails?Task.FromException(failure):Task.CompletedTask;
        }
        if(fails)Assert.Same(failure,await Assert.ThrowsAsync<IOException>(()=>host.MigrateDatabaseAsync<ProductContext>(Seed,token)));
        else Assert.Same(host,await host.MigrateDatabaseAsync<ProductContext>(Seed,token));
        Assert.True(invoked);
    }

    [Fact]
    public async Task Host_configuration_loads_base_and_environment_files()
    {
        var directory=Path.Combine(Path.GetTempPath(),"product-api-config-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var baseFile=Path.Combine(directory,"appsettings.json");var environmentFile=Path.Combine(directory,"appsettings.Testing.json");
        try{
            await File.WriteAllTextAsync(baseFile,"{\"Marker\":\"base\"}",TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(environmentFile,"{\"Marker\":\"environment\"}",TestContext.Current.CancellationToken);
            var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Testing",ContentRootPath=directory,ApplicationName=typeof(ServiceExtensions).Assembly.FullName});
            builder.Configuration.SetBasePath(directory);builder.Host.AddAppConfigurations();
            await using var app=builder.Build();Assert.Equal("environment",app.Configuration["Marker"]);
        }
        finally{File.Delete(baseFile);File.Delete(environmentFile);Directory.Delete(directory);}
    }
}
