using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmkDoc.Application.Common.Constants;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Infrastructure.Security;

public sealed class JwtTokenGenerator(IConfiguration configuration, TimeProvider timeProvider) : IJwtTokenGenerator
{
    public string GenerateToken(User user, Guid? projectId = null, IEnumerable<string>? roles = null)
    {
        var secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret is missing.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new("SystemRole", user.SystemRole.Name)
        };

        if (projectId.HasValue && projectId.Value != Guid.Empty)
        {
            claims.Add(new("ProjectId", projectId.Value.ToString()));
        }

        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new(ClaimTypes.Role, role));
            }
        }

        int accessTokenMinutes = configuration.GetValue<int?>("Jwt:AccessTokenMinutes")
            ?? AuthConstants.DefaultAccessTokenMinutes;
        if (accessTokenMinutes < 1 || accessTokenMinutes > AuthConstants.MaxAccessTokenMinutes)
        {
            throw new InvalidOperationException(
                $"Jwt:AccessTokenMinutes must be between 1 and {AuthConstants.MaxAccessTokenMinutes} minutes (configured: {accessTokenMinutes}).");
        }

        var expires = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(accessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
