using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Events;

/// <summary>
/// Domain event raised when a new document version is generated and attached to a Document aggregate root.
/// </summary>
public sealed record class DocumentVersionCreatedDomainEvent(
    DocumentId DocumentId,
    ProjectId ProjectId,
    int Version,
    Guid? TemplateVersionId,
    Guid? GenerationLogId,
    DateTimeOffset OccurredOn) : IDomainEvent;
