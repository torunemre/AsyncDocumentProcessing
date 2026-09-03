using AsyncDocumentProcessing.Application.DTOs;
using AsyncDocumentProcessing.Application.Interfaces;
using AsyncDocumentProcessing.Domain.Entities;
using AsyncDocumentProcessing.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncDocumentProcessing.Application.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IFileStorage _fileStorage;
        private readonly IDocumentQueue _documentQueue;

        public DocumentService(
            IDocumentRepository documentRepository,
            IFileStorage fileStorage,
            IDocumentQueue documentQueue)
        {
            _documentRepository = documentRepository;
            _fileStorage = fileStorage;
            _documentQueue = documentQueue;
        }

        public async Task<UploadDocumentResponse> UploadAsync(
    UploadDocumentRequest request,
    Stream fileStream,
    string fileName,
    CancellationToken cancellationToken = default)
        {
            var documentId = Guid.NewGuid();

            var filePath = await _fileStorage.SaveAsync(
                fileStream,
                fileName,
                cancellationToken);

            var document = new Document
            {
                Id = documentId,
                FileName = fileName,
                FilePath = filePath,
                DocumentType = request.DocumentType,
                BatchId = request.BatchId,
                SourceSystem = request.SourceSystem,
                Status = DocumentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await _documentRepository.AddAsync(
                document,
                cancellationToken);

            await _documentQueue.EnqueueAsync(
                documentId,
                cancellationToken);

            return new UploadDocumentResponse
            {
                TrackingId = documentId
            };
        }

        public async Task<DocumentResponse?> GetByIdAsync(
    Guid id,
    CancellationToken cancellationToken = default)
        {
            var document = await _documentRepository.GetByIdAsync(
                id,
                cancellationToken);

            if (document is null)
            {
                return null;
            }

            return new DocumentResponse
            {
                Id = document.Id,
                FileName = document.FileName,
                DocumentType = document.DocumentType,
                BatchId = document.BatchId,
                SourceSystem = document.SourceSystem,
                Status = document.Status,
                PageCount = document.PageCount,
                WordCount = document.WordCount,
                Sha256Hash = document.Sha256Hash,
                ExtractedText = document.ExtractedText,
                ErrorMessage = document.ErrorMessage,
                CreatedAt = document.CreatedAt,
                ProcessingStartedAt = document.ProcessingStartedAt,
                CompletedAt = document.CompletedAt
            };
        }

        public async Task<PagedResult<BatchDocumentResponse>> GetByBatchIdAsync(
    string batchId,
    int page,
    int pageSize,
    CancellationToken cancellationToken = default)
        {
            var result = await _documentRepository.GetByBatchIdAsync(
                batchId,
                page,
                pageSize,
                cancellationToken);

            var items = result.Items
                .Select(document => new BatchDocumentResponse
                {
                    Id = document.Id,
                    FileName = document.FileName,
                    DocumentType = document.DocumentType,
                    Status = document.Status,
                    PageCount = document.PageCount,
                    WordCount = document.WordCount,
                    CreatedAt = document.CreatedAt,
                    CompletedAt = document.CompletedAt
                })
                .ToList();

            return new PagedResult<BatchDocumentResponse>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.TotalCount
            };
        }
    }
}
