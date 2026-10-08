namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

public record ListProjectsQuery(
    Guid UserId,
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 20
);
