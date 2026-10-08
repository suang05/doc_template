namespace SmkDoc.Api.Contracts.IdentityAccess.Auth;

/// <summary>
/// Request payload for renewing access tokens using a refresh token.
/// </summary>
public record RefreshTokenRequest(string RefreshToken);
