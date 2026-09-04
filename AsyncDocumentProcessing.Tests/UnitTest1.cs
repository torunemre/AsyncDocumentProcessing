using AsyncDocumentProcessing.Application.DTOs;
using AsyncDocumentProcessing.Application.Interfaces;
using AsyncDocumentProcessing.Application.Services;
using AsyncDocumentProcessing.Domain.Entities;
using AsyncDocumentProcessing.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AsyncDocumentProcessing.Tests
{
    public class UnitTest1
    {
        [Fact]
        public async Task UploadAsync_ShouldSaveDocumentAsPending()
        {
            // Arrange
            var repository = new FakeDocumentRepository();
            var fileStorage = new FakeFileStorage();


        var service = new DocumentService(
            repository,
            fileStorage);

            var request = new UploadDocumentRequest
            {
                DocumentType = "pdf",
                BatchId = "BATCH-001",
                SourceSystem = "Test"
            };

            using var stream = new MemoryStream(
                "test document"u8.ToArray());

            // Act
            var result = await service.UploadAsync(
                request,
                stream,
                "test.pdf");

            // Assert
            Assert.NotEqual(Guid.Empty, result.TrackingId);

            Assert.NotNull(repository.AddedDocument);

            Assert.Equal(
                result.TrackingId,
                repository.AddedDocument!.Id);

            Assert.Equal(
                "test.pdf",
                repository.AddedDocument.FileName);

            Assert.Equal(
                DocumentStatus.Pending,
                repository.AddedDocument.Status);

            Assert.Equal(
                "BATCH-001",
                repository.AddedDocument.BatchId);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnDocumentResponse_WhenDocumentExists()
        {
            // Arrange
            var documentId = Guid.NewGuid();

            var document = new Document
            {
                Id = documentId,
                FileName = "test.pdf",
                DocumentType = "pdf",
                BatchId = "BATCH-001",
                SourceSystem = "Test",
                Status = DocumentStatus.Completed,
                PageCount = 5,
                WordCount = 100,
                Sha256Hash = "abc123",
                ExtractedText = "Test extracted text",
                CreatedAt = DateTime.UtcNow,
                ProcessingStartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            var repository = new FakeDocumentRepository
            {
                DocumentToReturn = document
            };

            var fileStorage = new FakeFileStorage();

            var service = new DocumentService(
                repository,
                fileStorage);

            // Act
            var result = await service.GetByIdAsync(documentId);

            // Assert
            Assert.NotNull(result);

            Assert.Equal(documentId, result!.Id);
            Assert.Equal("test.pdf", result.FileName);
            Assert.Equal("pdf", result.DocumentType);
            Assert.Equal("BATCH-001", result.BatchId);
            Assert.Equal("Test", result.SourceSystem);
            Assert.Equal(DocumentStatus.Completed, result.Status);
            Assert.Equal(5, result.PageCount);
            Assert.Equal(100, result.WordCount);
            Assert.Equal("abc123", result.Sha256Hash);
            Assert.Equal("Test extracted text", result.ExtractedText);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNull_WhenDocumentDoesNotExist()
        {
            // Arrange
            var repository = new FakeDocumentRepository
            {
                DocumentToReturn = null
            };

            var fileStorage = new FakeFileStorage();

            var service = new DocumentService(
                repository,
                fileStorage);

            // Act
            var result = await service.GetByIdAsync(Guid.NewGuid());

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetByBatchIdAsync_ShouldReturnPagedDocuments()
        {
            // Arrange
            var documents = new List<Document>
        {
            new Document
            {
                Id = Guid.NewGuid(),
                FileName = "document1.pdf",
                DocumentType = "pdf",
                Status = DocumentStatus.Completed,
                PageCount = 2,
                WordCount = 50,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            },
            new Document
            {
                Id = Guid.NewGuid(),
                FileName = "document2.pdf",
                DocumentType = "pdf",
                Status = DocumentStatus.Processing,
                PageCount = 1,
                WordCount = 20,
                CreatedAt = DateTime.UtcNow
            }
        };

            var repository = new FakeDocumentRepository
            {
                BatchDocumentsToReturn = documents,
                BatchTotalCount = 5
            };

            var fileStorage = new FakeFileStorage();

            var service = new DocumentService(
                repository,
                fileStorage);

            // Act
            var result = await service.GetByBatchIdAsync(
                "BATCH-001",
                2,
                10);

            // Assert
            Assert.NotNull(result);

            Assert.Equal(2, result.Page);
            Assert.Equal(10, result.PageSize);
            Assert.Equal(5, result.TotalCount);

            Assert.Equal(2, result.Items.Count);

            Assert.Equal(
                "document1.pdf",
                result.Items[0].FileName);

            Assert.Equal(
                DocumentStatus.Completed,
                result.Items[0].Status);

            Assert.Equal(
                "document2.pdf",
                result.Items[1].FileName);

            Assert.Equal(
                DocumentStatus.Processing,
                result.Items[1].Status);
        }

        [Fact]
        public async Task ProcessAsync_ShouldCompleteDocument_WhenProcessingSucceeds()
        {
            // Arrange
            var documentId = Guid.NewGuid();

            var document = new Document
            {
                Id = documentId,
                FileName = "test.pdf",
                FilePath = "Storage/test.pdf",
                DocumentType = "pdf",
                Status = DocumentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            var repository = new FakeDocumentRepository
            {
                DocumentToReturn = document
            };

            var fileStorage = new FakeFileStorage();
            var ocrService = new FakeOcrService();

            var options = Microsoft.Extensions.Options.Options.Create(
                new AsyncDocumentProcessing.Application.Options.DocumentProcessingOptions
                {
                    MaxRetryCount = 3
                });

            using var loggerFactory =
                Microsoft.Extensions.Logging.LoggerFactory.Create(
                    builder => { });

            var logger =
                loggerFactory.CreateLogger<
                    AsyncDocumentProcessing.Infrastructure.Processing.DocumentProcessor>();

            var processor =
                new AsyncDocumentProcessing.Infrastructure.Processing.DocumentProcessor(
                    repository,
                    fileStorage,
                    ocrService,
                    options,
                    logger);

            // Act
            await processor.ProcessAsync(documentId);

            // Assert
            Assert.Equal(
                DocumentStatus.Completed,
                repository.UpdatedDocument!.Status);

            Assert.Equal(
                1,
                repository.UpdatedDocument.PageCount);

            Assert.True(
                repository.UpdatedDocument.WordCount > 0);

            Assert.False(
                string.IsNullOrWhiteSpace(
                    repository.UpdatedDocument.Sha256Hash));

            Assert.False(
                string.IsNullOrWhiteSpace(
                    repository.UpdatedDocument.ExtractedText));

            Assert.NotNull(
                repository.UpdatedDocument.CompletedAt);
        }

        [Fact]
        public async Task ProcessAsync_ShouldFailDocument_WhenRetryLimitIsReached()
        {
            // Arrange
            var documentId = Guid.NewGuid();

            var document = new Document
            {
                Id = documentId,
                FileName = "retry-test.pdf",
                FilePath = "Storage/retry-test.pdf",
                DocumentType = "retry-test",
                Status = DocumentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            var repository = new FakeDocumentRepository
            {
                DocumentToReturn = document
            };

            var fileStorage = new FakeFileStorage();
            var ocrService = new FakeOcrService();

            var options = Microsoft.Extensions.Options.Options.Create(
                new AsyncDocumentProcessing.Application.Options.DocumentProcessingOptions
                {
                    MaxRetryCount = 3
                });

            using var loggerFactory =
                Microsoft.Extensions.Logging.LoggerFactory.Create(
                    builder => { });

            var logger =
                loggerFactory.CreateLogger<
                    AsyncDocumentProcessing.Infrastructure.Processing.DocumentProcessor>();

            var processor =
                new AsyncDocumentProcessing.Infrastructure.Processing.DocumentProcessor(
                    repository,
                    fileStorage,
                    ocrService,
                    options,
                    logger);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => processor.ProcessAsync(documentId));

            Assert.Equal(
                DocumentStatus.Failed,
                repository.UpdatedDocument!.Status);

            Assert.Equal(
                3,
                repository.UpdatedDocument.RetryCount);

            Assert.False(
                string.IsNullOrWhiteSpace(
                    repository.UpdatedDocument.ErrorMessage));
        }
    }

    public class FakeDocumentRepository : IDocumentRepository
    {
        public Document? AddedDocument { get; private set; }

        public Document? DocumentToReturn { get; set; }

        public IReadOnlyList<Document> BatchDocumentsToReturn { get; set; }
            = new List<Document>();

        public int BatchTotalCount { get; set; }

        public Document? UpdatedDocument { get; private set; }

        public Task<Document?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DocumentToReturn);
        }

        public Task<IReadOnlyList<Document>> GetPendingAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                (IReadOnlyList<Document>)new List<Document>());
        }

        public Task<bool> TryClaimAsync(
    Guid documentId,
    DateTime processingStartedAt,
    CancellationToken cancellationToken = default)
        {
            if (DocumentToReturn is null ||
                DocumentToReturn.Id != documentId ||
                DocumentToReturn.Status != DocumentStatus.Pending)
            {
                return Task.FromResult(false);
            }

            DocumentToReturn.Status = DocumentStatus.Processing;
            DocumentToReturn.ProcessingStartedAt = processingStartedAt;

            return Task.FromResult(true);
        }

        public Task<int> RecoverStaleProcessingAsync(
            DateTime staleBefore,
            CancellationToken cancellationToken = default)
        {
            if (DocumentToReturn is null ||
                DocumentToReturn.Status != DocumentStatus.Processing ||
                DocumentToReturn.ProcessingStartedAt is null ||
                DocumentToReturn.ProcessingStartedAt >= staleBefore)
            {
                return Task.FromResult(0);
            }

            DocumentToReturn.Status = DocumentStatus.Pending;
            DocumentToReturn.ProcessingStartedAt = null;

            return Task.FromResult(1);
        }

        public Task AddAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            AddedDocument = document;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            UpdatedDocument = document;
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<Document> Items, int TotalCount)>
            GetByBatchIdAsync(
                string batchId,
                int page,
                int pageSize,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                (
                    BatchDocumentsToReturn,
                    BatchTotalCount
                ));
        }
    }

    public class FakeFileStorage : IFileStorage
    {
        public Task<string> SaveAsync(
            Stream fileStream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                $"Storage/{fileName}");
        }

        public Task<Stream> OpenReadAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            Stream stream = new MemoryStream(
                "test"u8.ToArray());

            return Task.FromResult(stream);
        }
    }

    public class FakeOcrService : IOcrService
    {
        public Task<(string ExtractedText, int PageCount)> ProcessAsync(
            Stream fileStream,
            string fileExtension,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                (
                    "Bu metin fake OCR tarafýndan oluþturulmuþtur.",
                    1
                ));
        }
    }

}
