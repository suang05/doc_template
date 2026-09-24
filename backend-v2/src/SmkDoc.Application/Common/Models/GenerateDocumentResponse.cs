namespace SmkDoc.Application.Common.Models;

public record GenerateDocumentResponse(
    string Url,
    DateTimeOffset ExpiresAt,
    Guid GenerationId,
    string OutputFormat
);
