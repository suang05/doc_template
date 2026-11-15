namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

public sealed record ListProjectsQuery(
    Guid UserId,
    string? SearchTerm = null,
    int Page = 1,
    int Limit = 20,
    bool IncludeTotal = false
);
