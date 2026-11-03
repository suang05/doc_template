using Microsoft.Extensions.Configuration;

namespace SmkDoc.Infrastructure.Cache;

public static class RedisConfigurationResolver
{
    public const string DefaultInstancePrefix = "smkdoc:";

    public static string? ResolveConnectionString(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Redis")
            ?? configuration["Redis:ConnectionString"]
            ?? configuration["REDIS_URL"];

        return string.IsNullOrWhiteSpace(connectionString)
            ? null
            : connectionString.Trim();
    }

    public static string ResolveInstancePrefix(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var prefix = configuration["Redis:InstancePrefix"];
        return string.IsNullOrWhiteSpace(prefix)
            ? DefaultInstancePrefix
            : prefix.Trim();
    }
}
