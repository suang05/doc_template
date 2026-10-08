using System.Security.Cryptography;
using System.Text;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;

public static class RefreshTokenHelper
{
    public static string GenerateTokenString()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    }

    public static Sha256Hash HashToken(string token)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return new Sha256Hash(Convert.ToHexString(bytes).ToLowerInvariant());
    }
}
