using AsyncDocumentProcessing.Infrastructure.DependencyInjection;
using AsyncDocumentProcessing.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection bulunamadý.");

var storagePath = Path.Combine(
    builder.Environment.ContentRootPath,
    "Storage");

builder.Services.AddInfrastructure(
    connectionString,
    storagePath,
    queueCapacity: 1000);

builder.Services.AddHostedService<Worker>();


builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
