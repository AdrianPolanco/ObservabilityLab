using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObservabilityLab.Shared.Database.Seeding;

namespace ObservabilityLab.Shared.Database.Extensions
{
    public static class DatabaseSharedExtensions
    {
        public static IServiceCollection AddSharedDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
            services.AddScoped<DatabaseSeeder>();
            return services;
        }

        public static async Task SeedDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            using var scope = serviceProvider.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
}
}
