using AsyncDocumentProcessing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncDocumentProcessing.Application.Interfaces;
using AsyncDocumentProcessing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsyncDocumentProcessing.Infrastructure.Persistence.Repositories
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly AppDbContext _context;

        public DocumentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Document?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<(IReadOnlyList<Document> Items, int TotalCount)> GetByBatchIdAsync(
            string batchId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Documents
                .AsNoTracking()
                .Where(x => x.BatchId == batchId)
                .OrderByDescending(x => x.CreatedAt);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task AddAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            await _context.Documents.AddAsync(
                document,
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            _context.Documents.Update(document);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
