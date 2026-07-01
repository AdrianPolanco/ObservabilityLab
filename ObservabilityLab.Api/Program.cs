using ObservabilityLab.Api.Extensions;
using ObservabilityLab.Api.Features.Orders;
using ObservabilityLab.Shared.Database.Extensions;
using ObservabilityLab.Shared.Messaging.Extensions;
using ObservabilityLab.Shared.Observability;
using Serilog;

Log.Logger = ObservabilityExtensions.CreateStartupLogger();
var builder = WebApplication.CreateBuilder(args);

try
{
    Log.Information("Starting server...");
    builder.Services.AddOpenApi();
    builder.Services.AddSharedDatabase(builder.Configuration).AddServices();
    builder.Services.AddSharedMessaging(builder.Configuration);
    builder.Services.AddObservability(builder.Configuration);

    var app = builder.Build();

    app.UseExceptionHandler();

    // Seed database on startup if empty
    await app.Services.SeedDatabaseAsync();

    // Declare RabbitMQ topology idempotently on startup
    await app.Services.EnsureRabbitMqTopologyAsync();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();

    app.MapOrderEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}