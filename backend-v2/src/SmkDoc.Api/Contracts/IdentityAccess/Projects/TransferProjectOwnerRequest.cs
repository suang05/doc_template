namespace SmkDoc.Api.Contracts.IdentityAccess.Projects;

/// <summary>
/// Payload to transfer project ownership / Master Credential authority to a new user.
/// </summary>
public sealed record TransferProjectOwnerRequest(Guid NewOwnerUserId);

