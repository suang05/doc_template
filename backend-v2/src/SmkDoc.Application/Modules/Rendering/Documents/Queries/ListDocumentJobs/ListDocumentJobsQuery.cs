namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.ListDocumentJobs;

public sealed record ListDocumentJobsQuery(
    string? Status = null,
    string? Cursor = null,
    int Limit = 20);
