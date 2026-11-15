namespace SmkDoc.Application.Modules.TenantManagement.Projects.Queries.ListControlPlaneProjects;

public sealed record ListControlPlaneProjectsQuery(
    string? SearchTerm = null,
    int Page = 1,
    int Limit = 20,
    bool IncludeTotal = false);
