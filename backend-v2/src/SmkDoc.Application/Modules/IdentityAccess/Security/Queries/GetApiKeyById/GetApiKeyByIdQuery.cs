namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetApiKeyById;

public sealed record GetApiKeyByIdQuery(Guid KeyId, Guid? ProjectId = null);
