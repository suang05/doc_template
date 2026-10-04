namespace SmkDoc.Api.Contracts.IdentityAccess.Auth;

public record LoginRequest(string Email, string Password, Guid? ProjectId = null);
