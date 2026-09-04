using AsyncDocumentProcessing.Application.Options;
using AsyncDocumentProcessing.Infrastructure.DependencyInjection;
using AsyncDocumentProcessing.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<DocumentProcessingOptions>(
    builder.Configuration.GetSection("DocumentProcessing"));

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection bulunamadý.");

var storagePath = Path.Combine(
    builder.Environment.ContentRootPath,
    "Storage");

builder.Services.AddInfrastructure(
    connectionString,
    storagePath);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();