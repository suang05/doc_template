using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Infrastructure.Security;

public class JwtTokenGenerator(IConfiguration configuration, TimeProvider? timeProvider = null) : IJwtTokenGenerator
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

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

        var expires = _timeProvider.GetUtcNow().UtcDateTime.AddHours(24);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

