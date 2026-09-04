using AsyncDocumentProcessing.Application.Interfaces;
using AsyncDocumentProcessing.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AsyncDocumentProcessing.Worker;

public class Worker : BackgroundService
{
    private static readonly TimeSpan StaleProcessingTimeout =
        TimeSpan.FromMinutes(10);

    private static readonly TimeSpan StaleRecoveryInterval =
        TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Worker> _logger;
    private readonly DocumentProcessingOptions _options;

    public Worker(
        IServiceScopeFactory scopeFactory,
        IOptions<DocumentProcessingOptions> options,
        ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Document Worker started. MaxConcurrency: {MaxConcurrency}",
            _options.MaxConcurrency);

        var nextRecoveryAt = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTime.UtcNow >= nextRecoveryAt)
                {
                    await RecoverStaleDocumentsAsync(stoppingToken);

                    nextRecoveryAt =
                        DateTime.UtcNow + StaleRecoveryInterval;
                }

                IReadOnlyList<Guid> pendingDocumentIds;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var documentRepository =
                        scope.ServiceProvider
                            .GetRequiredService<IDocumentRepository>();

                    var pendingDocuments =
                        await documentRepository.GetPendingAsync(
                            stoppingToken);

                    pendingDocumentIds =
                        pendingDocuments
                            .Select(x => x.Id)
                            .ToList();

                    _logger.LogInformation(
                        "Pending document count: {Count}",
                        pendingDocumentIds.Count);
                }

                if (pendingDocumentIds.Count == 0)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(2),
                        stoppingToken);

                    continue;
                }

                await Parallel.ForEachAsync(
                    pendingDocumentIds,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism =
                            Math.Max(1, _options.MaxConcurrency),

                        CancellationToken =
                            stoppingToken
                    },
                    async (documentId, cancellationToken) =>
                    {
                        await ProcessDocumentAsync(
                            documentId,
                            cancellationToken);
                    });
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
                    "An error occurred while running the document worker.");

                await Task.Delay(
                    TimeSpan.FromSeconds(2),
                    stoppingToken);
            }
        }

        _logger.LogInformation(
            "Document Worker stopped.");
    }

    private async Task ProcessDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var documentRepository =
            scope.ServiceProvider
                .GetRequiredService<IDocumentRepository>();

        var documentProcessor =
            scope.ServiceProvider
                .GetRequiredService<IDocumentProcessor>();

        var processingStartedAt = DateTime.UtcNow;

        var claimed =
            await documentRepository.TryClaimAsync(
                documentId,
                processingStartedAt,
                cancellationToken);

        if (!claimed)
        {
            _logger.LogDebug(
                "Document could not be claimed because it is no longer pending: {DocumentId}",
                documentId);

            return;
        }

        _logger.LogInformation(
            "Document claimed for processing: {DocumentId}",
            documentId);

        try
        {
            await documentProcessor.ProcessAsync(
                documentId,
                cancellationToken);

            _logger.LogInformation(
                "Document processing completed: {DocumentId}",
                documentId);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Document processing failed after retries: {DocumentId}",
                documentId);
        }
    }

    private async Task RecoverStaleDocumentsAsync(
        CancellationToken cancellationToken)
    {
        var staleBefore =
            DateTime.UtcNow - StaleProcessingTimeout;

        using var scope = _scopeFactory.CreateScope();

        var documentRepository =
            scope.ServiceProvider
                .GetRequiredService<IDocumentRepository>();

        var recoveredCount =
            await documentRepository.RecoverStaleProcessingAsync(
                staleBefore,
                cancellationToken);

        if (recoveredCount > 0)
        {
            _logger.LogWarning(
                "Recovered {RecoveredCount} stale processing documents.",
                recoveredCount);
        }
    }
}