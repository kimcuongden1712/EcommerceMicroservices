using ObjectStorage.S3.Extensions;
using Product.API.Extensions;
using Product.API.Persistence;
using Serilog;
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();
var builder = WebApplication.CreateBuilder(args);
Log.Information($"Start {builder.Environment.ApplicationName} up");
try {
    builder.Host.AddAppConfigurations();
    // Add services to the container.
    builder.Services.AddConfigurationSettings(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);

    // Register Phase 3 shared storage
    builder.Services.AddS3ObjectStorage(builder.Configuration);

    builder.Services.AddProductImageServices(builder.Configuration);

    var app = builder.Build();
    app.UseInfrastructure();
    await app.MigrateDatabaseAsync<ProductContext>(async (context, _, _) =>
    {
        if (builder.Environment.IsDevelopment())
            await ProductContextSeed.SeedProductAsync(context, Log.Logger);
    });
    await app.RunAsync();
}
catch (Exception ex) {
    var type = ex.GetType().Name;
    if (type.Equals("StopTheHostException", StringComparison.Ordinal) ||
        type.Equals("HostAbortedException", StringComparison.Ordinal))
        throw;

    Log.Fatal(ex, $"Unhandled exception: {ex.Message}");
    throw;
}
finally {
    Log.Information($"Shutdown {builder.Environment.ApplicationName} complete");
    Log.CloseAndFlush();
}
