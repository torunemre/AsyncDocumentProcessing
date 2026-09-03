using AsyncDocumentProcessing.Application.Interfaces;
using AsyncDocumentProcessing.Application.Options;
using AsyncDocumentProcessing.Domain.Entities;
using AsyncDocumentProcessing.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace AsyncDocumentProcessing.Infrastructure.Processing;

public class DocumentProcessor : IDocumentProcessor
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IOcrService _ocrService;
    private readonly DocumentProcessingOptions _options;
    private readonly ILogger<DocumentProcessor> _logger;

    public DocumentProcessor(
        IDocumentRepository documentRepository,
        IFileStorage fileStorage,
        IOcrService ocrService,
        IOptions<DocumentProcessingOptions> options,
        ILogger<DocumentProcessor> logger)
    {
        _documentRepository = documentRepository;
        _fileStorage = fileStorage;
        _ocrService = ocrService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(
            documentId,
            cancellationToken);

        if (document is null)
        {
            throw new InvalidOperationException(
                $"Document bulunamadı: {documentId}");
        }

        while (true)
        {
            try
            {
                document.Status = DocumentStatus.Processing;
                document.ProcessingStartedAt = DateTime.UtcNow;
                document.ErrorMessage = null;
                document.LastErrorMessage = null;

                if (document.DocumentType.Equals(
                    "retry-test",
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Retry test amacıyla oluşturulan simüle hata.");
                }

                await _documentRepository.UpdateAsync(
                    document,
                    cancellationToken);

                _logger.LogInformation(
                    "Document file processing started: {DocumentId}",
                    document.Id);

                await using var stream =
                    await _fileStorage.OpenReadAsync(
                        document.FilePath,
                        cancellationToken);

                // 1. Adım: SHA-256 hesapla
                document.Sha256Hash =
                    await CalculateSha256Async(
                        stream,
                        cancellationToken);

                // SHA-256 stream'i sona getirdiği için
                // OCR öncesinde başa dönüyoruz.
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                _logger.LogInformation(
                    "Document OCR started: {DocumentId}",
                    document.Id);

                // 2. Adım: Gerçek OCR
                var (extractedText, pageCount) =
    await _ocrService.ProcessAsync(
        stream,
        Path.GetExtension(document.FileName),
        cancellationToken);

                document.PageCount = pageCount;
                document.ExtractedText = extractedText;
                document.WordCount = CountWords(extractedText);

                _logger.LogInformation(
                    "Document OCR completed: {DocumentId}. PageCount: {PageCount}, WordCount: {WordCount}",
                    document.Id,
                    document.PageCount,
                    document.WordCount);

                // 3. Adım: İşlem tamamlandı
                document.Status = DocumentStatus.Completed;
                document.CompletedAt = DateTime.UtcNow;
                document.ErrorMessage = null;
                document.LastErrorMessage = null;

                await _documentRepository.UpdateAsync(
                    document,
                    cancellationToken);

                return;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                document.LastErrorMessage = ex.Message;
                document.ErrorMessage = ex.Message;

                if (document.RetryCount < _options.MaxRetryCount)
                {
                    document.RetryCount++;

                    document.Status = DocumentStatus.Processing;

                    _logger.LogWarning(
                        "Document retrying: {DocumentId}. Retry {RetryCount}/{MaxRetryCount}. Error: {ErrorMessage}",
                        document.Id,
                        document.RetryCount,
                        _options.MaxRetryCount,
                        ex.Message);

                    await _documentRepository.UpdateAsync(
                        document,
                        cancellationToken);

                    continue;
                }

                document.Status = DocumentStatus.Failed;

                await _documentRepository.UpdateAsync(
                    document,
                    cancellationToken);

                throw;
            }
        }
    }

    private static async Task<string> CalculateSha256Async(
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var sha256 = SHA256.Create();

        var hash = await sha256.ComputeHashAsync(
            stream,
            cancellationToken);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text
            .Split(
                Array.Empty<char>(),
                StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }
}