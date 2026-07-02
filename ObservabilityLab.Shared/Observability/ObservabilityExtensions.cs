

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Diagnostics;

namespace ObservabilityLab.Shared.Observability
{
    public static class ObservabilityExtensions
    {

        public static ILogger CreateStartupLogger() => new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

        /// <summary>
        /// Wires up tracing (+ Serilog) for a service.
        /// <para>
        /// <paramref name="addAspNetCoreInstrumentation"/> should only be <see langword="true"/> for the
        /// ASP.NET Core API — the three worker services are plain <c>BackgroundService</c> hosts with no
        /// inbound HTTP pipeline, so that instrumentation would register listeners that never fire.
        /// </para>
        /// </summary>
        public static IServiceCollection AddObservability(
            this IServiceCollection services,
            IConfiguration configuration,
            string serviceName,
            bool addAspNetCoreInstrumentation = false)
        {
            // The W3C "traceparent" format is what lets a trace cross a transport that has no ambient
            // Activity.Current — like a RabbitMQ message. Setting it explicitly (rather than relying on the
            // implicit default) is what RabbitMqPublisher/RabbitMqConsumer use to Inject/Extract context
            // across the queue hop, re-linking all 4 services into a single trace.
            Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());

            // ActivitySource is .NET's built-in stand-in for an OTel "Tracer" — it's how application code
            // starts spans (StartActivity). The OTel SDK doesn't create it; it just subscribes to it via
            // .AddSource(serviceName) below. Registered as a singleton so handlers/services can inject it.
            services.AddSingleton((serviceProvider) => new ActivitySource(serviceName));

            services.AddOpenTelemetry()
                // "Resource" attributes describe the process emitting the telemetry. service.name is what
                // makes each of the 4 processes show up as a distinct node in the Tempo/Grafana trace view.
                .ConfigureResource(resource => resource.AddService(serviceName))
                .WithTracing(tracing =>
                {
                    tracing
                        // Subscribes the SDK to spans started via our own ActivitySource above (manual/business spans).
                        .AddSource(serviceName)
                        // Auto-instrumentation: EF Core ships its own ActivitySource internally; this just
                        // turns it on, giving a Client span per SQL command with zero hand-written code.
                        .AddEntityFrameworkCoreInstrumentation()
                        .AddNpgsql()
                        // Auto-instrumentation: every outbound HttpClient call (e.g. the MinIO SDK, which is
                        // itself built on HttpClient) becomes a Client span for free.
                        .AddHttpClientInstrumentation();

                    if (addAspNetCoreInstrumentation)
                    {
                        // Auto-instrumentation: wraps every inbound HTTP request in a Server span — the root
                        // of the trace for anything that starts at the API.
                        tracing.AddAspNetCoreInstrumentation();
                    }

                    tracing.AddOtlpExporter(tracingExporter =>
                    {
                        // Apps never talk to Tempo directly — they export to the OTel Collector, which
                        // forwards to Tempo. Keeps the collector as the single choke point for
                        // batching/processing/fan-out.
                        tracingExporter.Endpoint = new Uri("http://localhost:4317");
                        tracingExporter.Protocol = OtlpExportProtocol.Grpc;
                    });
                });

            services.AddSerilog((services, loggerConfiguration)
                => loggerConfiguration
                       .ReadFrom.Configuration(configuration)
                       .ReadFrom.Services(services));

            return services;
        }

        public static void UseRequestLogging(this WebApplication app)
        {
            app.UseSerilogRequestLogging(options =>
            {
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                    diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
                };
                options.GetLevel = (httpContext, elapsed, ex) =>
                {
                    if (httpContext.Request.Path.StartsWithSegments("/health"))
                        return Serilog.Events.LogEventLevel.Verbose;
                    return elapsed > 500 ? Serilog.Events.LogEventLevel.Warning : Serilog.Events.LogEventLevel.Information;
                };
            });
        }
    }
}
