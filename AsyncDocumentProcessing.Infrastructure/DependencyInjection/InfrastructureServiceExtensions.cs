using AsyncDocumentProcessing.Application.Interfaces;
using AsyncDocumentProcessing.Infrastructure.OCR;
using AsyncDocumentProcessing.Infrastructure.Persistence;
using AsyncDocumentProcessing.Infrastructure.Persistence.Repositories;
using AsyncDocumentProcessing.Infrastructure.Processing;
using AsyncDocumentProcessing.Infrastructure.Queue;
using AsyncDocumentProcessing.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace AsyncDocumentProcessing.Infrastructure.DependencyInjection
{
    public static class InfrastructureServiceExtensions
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            string connectionString,
            string storagePath,
            int queueCapacity = 1000)
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
            });

            services.AddScoped<IDocumentRepository, DocumentRepository>();

            services.AddScoped<IDocumentProcessor, DocumentProcessor>();

            services.AddScoped<IOcrService, TesseractOcrService>();

            services.AddSingleton<IFileStorage>(
                new LocalFileStorage(storagePath));

            services.AddSingleton<IDocumentQueue>(
                new DocumentQueue(queueCapacity));

            return services;
        }
    }
}
