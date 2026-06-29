using ObservabilityLab.Api.Extensions;
using ObservabilityLab.Api.Features.Orders;
using ObservabilityLab.Shared.Database.Extensions;
using ObservabilityLab.Shared.Messaging.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSharedDatabase(builder.Configuration).AddServices();
builder.Services.AddSharedMessaging(builder.Configuration);

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

app.UseHttpsRedirection();

app.MapOrderEndpoints();

app.Run();
