using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Common.Builders;

public class DocumentBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _documentRef = "DOC-2026-001";
    private Guid? _templateId = Guid.NewGuid();
    private DateTimeOffset _now = TestConstants.BaselineTime;
    private ProjectId _projectId = ProjectId.New();

    public static DocumentBuilder ADocument() => new();

    public static Document Default() => ADocument().Build();


    public DocumentBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public DocumentBuilder WithDocumentRef(string documentRef)
    {
        _documentRef = documentRef;
        return this;
    }

    public DocumentBuilder WithTemplateId(Guid? templateId)
    {
        _templateId = templateId;
        return this;
    }

    public DocumentBuilder WithTemplateId(TemplateId? templateId)
    {
        _templateId = templateId?.Value;
        return this;
    }

    public DocumentBuilder WithTime(DateTimeOffset now)
    {
        _now = now;
        return this;
    }

    public DocumentBuilder WithProjectId(ProjectId projectId)
    {
        _projectId = projectId;
        return this;
    }

    public Document Build()
    {
        return new Document(
            new DocumentId(_id),
            DocumentReference.Create(_documentRef),
            _templateId.HasValue ? new TemplateId(_templateId.Value) : null,
            _now,
            _projectId);
    }

    public static implicit operator Document(DocumentBuilder builder) => builder.Build();
}
