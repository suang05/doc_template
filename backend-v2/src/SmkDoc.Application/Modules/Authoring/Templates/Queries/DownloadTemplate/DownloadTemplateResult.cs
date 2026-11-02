namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;

public sealed record DownloadTemplateResult(
    Stream Stream,
    string ContentType,
    string FileName
);
