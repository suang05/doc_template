using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Builders;

public class DocumentBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _documentRef = "DOC-2026-001";
    private Guid? _templateId = Guid.NewGuid();
    private DateTimeOffset _now = TestConstants.BaselineTime;

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

    public DocumentBuilder WithTime(DateTimeOffset now)
    {
        _now = now;
        return this;
    }

    public Document Build()
    {
        return new Document(
            _id,
            DocumentReference.Create(_documentRef),
            _templateId,
            _now);
    }
}
