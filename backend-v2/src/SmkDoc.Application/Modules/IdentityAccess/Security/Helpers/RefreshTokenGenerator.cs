using System.Security.Cryptography;
using System.Text;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;

public static class RefreshTokenGenerator
{
    public static string GenerateTokenString()
    {
        return Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    }

    public static Sha256Hash HashToken(string token)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return new Sha256Hash(Convert.ToHexStringLower(bytes));
    }
}

