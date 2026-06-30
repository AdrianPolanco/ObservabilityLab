using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace ObservabilityLab.Shared.Services;

public static class ServicesExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        // WithEndpoint expects host + port without a scheme; SSL disabled for local HTTP.
        services.AddMinio(c =>
        {
            c.WithEndpoint("localhost", 9000);
            c.WithCredentials("observability", "observability");
            c.WithSSL(false);
        });

        services.AddScoped<MinIOService>();

        return services;
    }
}
