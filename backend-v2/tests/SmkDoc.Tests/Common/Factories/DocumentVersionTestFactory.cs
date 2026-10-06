using SmkDoc.Domain.Entities;

namespace SmkDoc.Tests.Common.Factories;

public static class DocumentVersionTestFactory
{
    public static DocumentVersion Create(
        Guid? id = null,
        Guid? documentId = null,
        int version = 1,
        Guid? templateVersionId = null,
        Guid? generationLogId = null,
        string? changeNote = null,
        string? createdBy = null,
        DateTimeOffset? now = null)
    {
        return new DocumentVersion(
            id ?? Guid.NewGuid(),
            documentId ?? Guid.NewGuid(),
            version,
            templateVersionId,
            generationLogId,
            changeNote,
            createdBy,
            now ?? TestConstants.BaselineTime);
    }
}
