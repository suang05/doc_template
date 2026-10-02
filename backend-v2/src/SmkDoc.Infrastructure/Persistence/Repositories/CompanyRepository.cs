using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class CompanyRepository(AppDbContext context) : ICompanyRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Company?> GetFirstAsync(CancellationToken ct = default)
    {
        return await _context.Companies.FirstOrDefaultAsync(ct);
    }

    public async Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Companies.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<List<Company>> ListAsync(CancellationToken ct = default)
    {
        return await _context.Companies.ToListAsync(ct);
    }

    public async Task AddAsync(Company company, CancellationToken ct = default)
    {
        await _context.Companies.AddAsync(company, ct);
    }
}
