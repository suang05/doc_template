namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.RenderStatelessDocument;

public sealed record RenderStatelessDocumentCommand(
    Stream FileStream,
    string FileName,
    string? JsonData = null
);
