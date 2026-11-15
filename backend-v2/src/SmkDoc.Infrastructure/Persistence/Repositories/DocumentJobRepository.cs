using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class DocumentJobRepository(AppDbContext context) : IDocumentJobRepository
{
    public async Task<DocumentJob?> GetByIdAsync(DocumentJobId id, CancellationToken ct)
    {
        return await context.DocumentJobs
            .FirstOrDefaultAsync(j => j.Id == id, ct);
    }

    public async Task<DocumentJob?> GetByIdWithItemsAsync(DocumentJobId id, CancellationToken ct)
    {
        return await context.DocumentJobs
            .Include(j => j.Items.OrderBy(i => i.ItemIndex))
            .FirstOrDefaultAsync(j => j.Id == id, ct);
    }

    public async Task AddAsync(DocumentJob job, CancellationToken ct)
    {
        await context.DocumentJobs.AddAsync(job, ct);
    }

    public void Update(DocumentJob job)
    {
        context.DocumentJobs.Update(job);
    }
}
