using ObservabilityLab.Api.Extensions;
using ObservabilityLab.Api.Features.Orders;
using ObservabilityLab.Shared.Database.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSharedDatabase(builder.Configuration).AddServices();

var app = builder.Build();

app.UseExceptionHandler();

// Seed database on startup if empty
await app.Services.SeedDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapOrderEndpoints();

app.Run();
