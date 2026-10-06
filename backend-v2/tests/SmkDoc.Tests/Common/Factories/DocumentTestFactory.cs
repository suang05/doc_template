using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Factories;

public static class DocumentTestFactory
{
    public static Document Create(
        Guid? id = null,
        string documentRef = "DOC-001",
        Guid? templateId = null,
        DateTimeOffset? now = null)
    {
        return new Document(
            id ?? Guid.NewGuid(),
            DocumentReference.Create(documentRef),
            templateId,
            now ?? DateTimeOffset.UtcNow);
    }
}
