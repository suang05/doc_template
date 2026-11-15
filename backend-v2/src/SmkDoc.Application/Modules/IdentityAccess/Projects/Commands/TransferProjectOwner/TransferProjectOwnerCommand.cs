namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.TransferProjectOwner;

public sealed record TransferProjectOwnerCommand(Guid ProjectId, Guid NewOwnerUserId, long ExpectedVersion = 1);

