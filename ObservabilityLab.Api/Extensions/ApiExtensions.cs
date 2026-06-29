using ObservabilityLab.Api.ExceptionHandling;
using ObservabilityLab.Api.Features.Orders;

namespace ObservabilityLab.Api.Extensions
{
    public static class ApiExtensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddExceptionHandler<ApplicationExceptionHandler>();
            services.AddProblemDetails();
            services.AddScoped<OrderService>();

            return services;
        }
    }
}
