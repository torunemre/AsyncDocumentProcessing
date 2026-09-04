using AsyncDocumentProcessing.Application.DTOs;
using System;


namespace AsyncDocumentProcessing.Application.Interfaces
{
    public interface IDocumentService
    {
        Task<UploadDocumentResponse> UploadAsync(
            UploadDocumentRequest request,
            Stream fileStream,
            string fileName,
            CancellationToken cancellationToken = default);

        Task<DocumentResponse?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<PagedResult<BatchDocumentResponse>> GetByBatchIdAsync(
            string batchId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}
