namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;

public sealed record RevokeApiKeyCommand(Guid Id, string? IfMatch = null);
