namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.DownloadGenerationLogDocument;

public sealed record DownloadGenerationLogDocumentResult(Stream Stream, string ContentType, string FileName);

