using AsyncDocumentProcessing.Application.Interfaces;
using AsyncDocumentProcessing.Domain.Entities;
using AsyncDocumentProcessing.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AsyncDocumentProcessing.Infrastructure.Persistence.Repositories;

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

    public async Task<IReadOnlyList<Document>> GetPendingAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .AsNoTracking()
            .Where(x => x.Status == DocumentStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryClaimAsync(
        Guid documentId,
        DateTime processingStartedAt,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _context.Documents
            .Where(x =>
                x.Id == documentId &&
                x.Status == DocumentStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.Status,
                        DocumentStatus.Processing)
                    .SetProperty(
                        x => x.ProcessingStartedAt,
                        processingStartedAt)
                    .SetProperty(
                        x => x.ErrorMessage,
                        (string?)null)
                    .SetProperty(
                        x => x.LastErrorMessage,
                        (string?)null),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<int> RecoverStaleProcessingAsync(
        DateTime staleBefore,
        CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .Where(x =>
                x.Status == DocumentStatus.Processing &&
                x.ProcessingStartedAt != null &&
                x.ProcessingStartedAt < staleBefore)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.Status,
                        DocumentStatus.Pending)
                    .SetProperty(
                        x => x.ProcessingStartedAt,
                        (DateTime?)null),
                cancellationToken);
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