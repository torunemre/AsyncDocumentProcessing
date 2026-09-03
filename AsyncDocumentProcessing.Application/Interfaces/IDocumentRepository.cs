using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncDocumentProcessing.Domain.Entities;

namespace AsyncDocumentProcessing.Application.Interfaces
{
    public interface IDocumentRepository
    {
        Task<Document?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<(IReadOnlyList<Document> Items, int TotalCount)> GetByBatchIdAsync(
            string batchId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            Document document,
            CancellationToken cancellationToken = default);

        Task UpdateAsync(
            Document document,
            CancellationToken cancellationToken = default);
    }
}
