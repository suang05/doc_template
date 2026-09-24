using System.Security.Cryptography;
using System.Text;

namespace SmkDoc.Infrastructure.Security;

public static class Sha256Hasher
{
    public static string Hash(string rawData)
    {
        using var sha256Hash = SHA256.Create();
        var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

        var builder = new StringBuilder();
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }
}
