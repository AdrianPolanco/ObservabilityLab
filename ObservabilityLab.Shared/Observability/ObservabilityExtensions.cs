

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ObservabilityLab.Shared.Observability
{
    public static class ObservabilityExtensions
    {

        public static ILogger CreateStartupLogger() => new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

        public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSerilog((services, loggerConfiguration) 
                => loggerConfiguration
                       .ReadFrom.Configuration(configuration)
                       .ReadFrom.Services(services));

            return services;
        }
    }
}
