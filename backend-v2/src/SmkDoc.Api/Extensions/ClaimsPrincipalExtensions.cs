using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Api.Extensions;

/// <summary>
/// Extension methods for <see cref="ClaimsPrincipal"/> used across JWT-authenticated controllers.
/// Centralises claim-parsing logic — Single Source of Truth for JWT claim names,
/// supporting both MapInboundClaims=false ("sub") and MapInboundClaims=true (ClaimTypes.*).
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extracts the authenticated user's ID from the JWT "sub" claim.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Thrown when claim is missing or not a valid GUID.</exception>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var id))
            throw new UnauthorizedAccessException("User ID claim is missing or invalid.");

        return id;
    }

    /// <summary>
    /// Asserts that the authenticated user holds the <see cref="RoleType.Admin"/> role.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user is not an Admin.</exception>
    public static void RequireAdmin(this ClaimsPrincipal user)
    {
        var role = user.FindFirstValue("role") ?? user.FindFirstValue(ClaimTypes.Role);

        if (role != nameof(RoleType.Admin))
            throw new UnauthorizedAccessException("Admin role required.");
    }
}
