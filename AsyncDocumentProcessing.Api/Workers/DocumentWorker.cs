using AsyncDocumentProcessing.Application.Interfaces;
using AsyncDocumentProcessing.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
namespace AsyncDocumentProcessing.Api.Workers;
using Microsoft.Extensions.Options;
using AsyncDocumentProcessing.Application.Options;

public class DocumentWorker : BackgroundService
{
    private readonly IDocumentQueue _documentQueue;
    //private readonly IDocumentRepository _documentRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentWorker> _logger;
    private readonly SemaphoreSlim _semaphore;
    private int _activeProcessingCount;


    public DocumentWorker(
    IDocumentQueue documentQueue,
    IServiceScopeFactory scopeFactory,
    IOptions<DocumentProcessingOptions> options,
    ILogger<DocumentWorker> logger)
    {
        _documentQueue = documentQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;

        _semaphore = new SemaphoreSlim(
            options.Value.MaxConcurrency);
    }

    protected override async Task ExecuteAsync(
    CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Document Worker started.");

        var tasks = new List<Task>();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var documentId = await _documentQueue.DequeueAsync(
                    stoppingToken);

                await _semaphore.WaitAsync(
                    stoppingToken);

                tasks.Add(
                    ProcessDocumentWithLimitAsync(
                        documentId,
                        stoppingToken));

                tasks.RemoveAll(task => task.IsCompleted);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await Task.WhenAll(tasks);

            _semaphore.Dispose();
        }

        _logger.LogInformation(
            "Document Worker stopped.");
    }

    private async Task ProcessDocumentWithLimitAsync(
     Guid documentId,
     CancellationToken cancellationToken)
    {
        var activeCount = Interlocked.Increment(
            ref _activeProcessingCount);

        _logger.LogInformation(
            "Active document processing count: {ActiveCount}",
            activeCount);

        try
        {
            await ProcessDocumentAsync(
                documentId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Document processing failed: {DocumentId}",
                documentId);
        }
        finally
        {
            activeCount = Interlocked.Decrement(
                ref _activeProcessingCount);

            _logger.LogInformation(
                "Active document processing count: {ActiveCount}",
                activeCount);

            _semaphore.Release();
        }
    }

    private async Task ProcessDocumentAsync(
      Guid documentId,
      CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var processor =
            scope.ServiceProvider
                .GetRequiredService<IDocumentProcessor>();

        _logger.LogInformation(
            "Document processing started: {DocumentId}",
            documentId);

        await processor.ProcessAsync(
            documentId,
            cancellationToken);

        _logger.LogInformation(
            "Document processing completed: {DocumentId}",
            documentId);
    }

}