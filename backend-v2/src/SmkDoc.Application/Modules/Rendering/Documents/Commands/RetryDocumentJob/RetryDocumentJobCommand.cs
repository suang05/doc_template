namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.RetryDocumentJob;

public sealed record RetryDocumentJobCommand(Guid JobId, long ExpectedVersion = 1);
