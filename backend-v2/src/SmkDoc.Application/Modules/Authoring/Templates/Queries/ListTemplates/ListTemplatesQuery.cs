namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

public sealed record ListTemplatesQuery(
    string? SearchTerm = null,
    string? Category = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20);
