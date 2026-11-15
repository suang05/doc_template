using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Queries;

public sealed class ProjectStatusReader(AppDbContext context) : IProjectStatusReader
{
    public async Task<ProjectStatusSnapshot?> FindProjectStatusAsync(ProjectId projectId, CancellationToken ct)
    {
        var project = await context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project is null)
        {
            return null;
        }

        return new ProjectStatusSnapshot(
            project.Id,
            project.Slug.Value,
            project.IsActive,
            project.GetAllowedEgressCidrList());
    }
}
