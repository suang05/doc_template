using System.Security.Cryptography;
using System.Text;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;

public static class ApiKeyHelper
{
    public static string ComputeHash(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
