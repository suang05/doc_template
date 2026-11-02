namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;

public sealed record DownloadDocumentVersionResult(Stream Stream, string ContentType, string FileName);
