namespace SmkDoc.Application.Common.Models;

public record DocumentVersionDto(
    Guid Id,
    Guid DocumentId,
    string DocumentRef,
    int Version,
    Guid? TemplateVersionId,
    Guid? GenerationLogId,
    string? ChangeNote,
    string? CreatedBy,
    DateTimeOffset CreatedAt
);
