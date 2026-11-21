using Common.Logging;
using Microsoft.EntityFrameworkCore;
using Serilog;
namespace Product.API.Extensions
{
    public static class HostExtensions
    {
        public static void AddAppConfigurations (this ConfigureHostBuilder host)
        {
            host.ConfigureAppConfiguration((context, config) =>
            {
                var env = context.HostingEnvironment;
                config.AddJsonFile("appsettings.json", false, true)
                    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true)
                    .AddEnvironmentVariables();
            }).UseSerilog(Serilogger.Configure);
        }

        public static async Task<IHost> MigrateDatabaseAsync<TContext> (
            this IHost host,
            Func<TContext, IServiceProvider, CancellationToken, Task> seeder,
            CancellationToken cancellationToken = default)
            where TContext : DbContext
        {
            await using var scope = host.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<TContext>>();
            var context = services.GetRequiredService<TContext>();

            try {
                logger.LogInformation("Migrating MySQL database.");
                await context.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Migrated MySQL database.");
                await seeder(context, services, cancellationToken);
            }
            catch (Exception ex) {
                logger.LogCritical(ex, "Database migration or seeding failed.");
                throw;
            }

            return host;
        }
    }
}
