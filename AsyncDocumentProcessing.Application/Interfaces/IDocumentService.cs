using AsyncDocumentProcessing.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncDocumentProcessing.Application.DTOs;

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
