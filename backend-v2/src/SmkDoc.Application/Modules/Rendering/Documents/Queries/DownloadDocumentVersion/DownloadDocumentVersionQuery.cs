namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;

public record DownloadDocumentVersionQuery(string DocumentRef, int Version);

public record DownloadDocumentVersionResult(Stream Stream, string ContentType, string FileName);
