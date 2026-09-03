using AsyncDocumentProcessing.Application.Interfaces;
using AsyncDocumentProcessing.Domain.Enums;
using AsyncDocumentProcessing.Infrastructure.Persistence.Repositories;

namespace AsyncDocumentProcessing.Worker
{
    public class Worker : BackgroundService
    {
        private readonly IDocumentQueue _documentQueue;
        private readonly ILogger<Worker> _logger;
        private readonly IDocumentRepository _documentRepository;

        public Worker(
    IDocumentQueue documentQueue,
    IDocumentRepository documentRepository,
    ILogger<Worker> logger)
        {
            _documentQueue = documentQueue;
            _documentRepository = documentRepository;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation("Document Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var documentId = await _documentQueue.DequeueAsync(
                        stoppingToken);

                    await ProcessDocumentAsync(
    documentId,
    stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "An error occurred while processing the document queue.");
                }
            }

            _logger.LogInformation("Document Worker stopped.");
        }

        private async Task ProcessDocumentAsync(
    Guid documentId,
    CancellationToken cancellationToken)
        {
            var document = await _documentRepository.GetByIdAsync(
                documentId,
                cancellationToken);

            if (document is null)
            {
                _logger.LogWarning(
                    "Document not found: {DocumentId}",
                    documentId);

                return;
            }

            document.Status = DocumentStatus.Processing;
            document.ProcessingStartedAt = DateTime.UtcNow;

            await _documentRepository.UpdateAsync(
                document,
                cancellationToken);

            _logger.LogInformation(
                "Document processing started: {DocumentId}",
                documentId);

            // Gerçek document processing/OCR iþlemi
            // bir sonraki aþamada buraya gelecek.

            document.Status = DocumentStatus.Completed;
            document.CompletedAt = DateTime.UtcNow;

            await _documentRepository.UpdateAsync(
                document,
                cancellationToken);

            _logger.LogInformation(
                "Document processing completed: {DocumentId}",
                documentId);
        }

    }
}
