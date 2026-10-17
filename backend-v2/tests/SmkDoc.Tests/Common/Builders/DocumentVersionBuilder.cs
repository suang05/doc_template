using SmkDoc.Domain.Entities;

namespace SmkDoc.Tests.Common.Builders;

public class DocumentVersionBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _documentId = Guid.NewGuid();
    private int _version = 1;
    private Guid? _templateVersionId = Guid.NewGuid();
    private Guid? _generationLogId = Guid.NewGuid();
    private string? _changeNote = "Initial version";
    private string? _createdBy = "system";
    private DateTimeOffset _now = TestConstants.BaselineTime;

    public static DocumentVersionBuilder AVersion() => new();

    public static DocumentVersion Default() => AVersion().Build();

    public static DocumentVersion Create(
        Guid? id = null,
        Guid? documentId = null,
        int version = 1,
        Guid? templateVersionId = null,
        Guid? generationLogId = null,
        string? changeNote = "Initial version",
        string? createdBy = "system",
        DateTimeOffset? now = null)
    {
        var builder = new DocumentVersionBuilder();
        if (id.HasValue) builder.WithId(id.Value);
        if (documentId.HasValue) builder.ForDocument(documentId.Value);
        builder.WithVersion(version);
        builder.WithTemplateVersionId(templateVersionId);
        builder.WithGenerationLogId(generationLogId);
        if (changeNote != null) builder.WithChangeNote(changeNote);
        if (createdBy != null) builder.WithCreatedBy(createdBy);
        if (now.HasValue) builder.WithTime(now.Value);
        return builder.Build();
    }

    public DocumentVersionBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public DocumentVersionBuilder ForDocument(Guid documentId)
    {
        _documentId = documentId;
        return this;
    }

    public DocumentVersionBuilder WithDocumentId(Guid documentId) => ForDocument(documentId);

    public DocumentVersionBuilder WithVersion(int version)
    {
        _version = version;
        return this;
    }

    public DocumentVersionBuilder WithTemplateVersionId(Guid? templateVersionId)
    {
        _templateVersionId = templateVersionId;
        return this;
    }

    public DocumentVersionBuilder WithGenerationLogId(Guid? generationLogId)
    {
        _generationLogId = generationLogId;
        return this;
    }

    public DocumentVersionBuilder WithChangeNote(string? changeNote)
    {
        _changeNote = changeNote;
        return this;
    }

    public DocumentVersionBuilder WithCreatedBy(string? createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    public DocumentVersionBuilder WithTime(DateTimeOffset now)
    {
        _now = now;
        return this;
    }

    public DocumentVersion Build()
    {
        return new DocumentVersion(
            _id,
            _documentId,
            _version,
            _templateVersionId,
            _generationLogId,
            _changeNote,
            _createdBy,
            _now);
    }
}
