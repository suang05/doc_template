namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;

public sealed record CreateApiKeyCommand(
    string Name,
    string CallerApp,
    Guid? ProjectId = null);
