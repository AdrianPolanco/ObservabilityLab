using ObservabilityLab.OrderProcessingWorker;
using ObservabilityLab.Shared.Database.Extensions;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddSharedDatabase(builder.Configuration);

var host = builder.Build();
host.Run();
