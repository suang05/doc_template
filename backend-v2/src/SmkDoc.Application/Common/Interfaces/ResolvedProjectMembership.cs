namespace SmkDoc.Application.Common.Interfaces;

public sealed record ResolvedProjectMembership(Guid ProjectId, Guid UserId, string RoleName);
