using System.Text.Json.Serialization;
using ObservabilityLab.Api.ExceptionHandling;
using ObservabilityLab.Api.Features.Orders;

namespace ObservabilityLab.Api.Extensions
{
    public static class ApiExtensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            services.AddExceptionHandler<ApplicationExceptionHandler>();
            services.AddProblemDetails();
            services.AddScoped<OrderService>();

            return services;
        }
    }
}
