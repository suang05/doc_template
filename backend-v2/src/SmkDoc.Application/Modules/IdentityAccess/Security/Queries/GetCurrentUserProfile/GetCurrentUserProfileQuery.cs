namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;

public sealed record GetCurrentUserProfileQuery(Guid UserId, Guid? ActiveProjectId = null);
