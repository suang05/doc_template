namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.CancelDocumentJob;

public sealed record CancelDocumentJobCommand(Guid JobId, long ExpectedVersion = 1);
