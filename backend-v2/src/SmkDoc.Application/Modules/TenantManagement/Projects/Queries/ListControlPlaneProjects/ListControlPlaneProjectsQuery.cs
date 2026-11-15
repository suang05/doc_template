namespace SmkDoc.Application.Modules.TenantManagement.Projects.Queries.ListControlPlaneProjects;

public sealed record ListControlPlaneProjectsQuery(
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 20);
