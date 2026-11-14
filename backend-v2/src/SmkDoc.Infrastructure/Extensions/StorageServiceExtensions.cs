using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Infrastructure.Storage;

namespace SmkDoc.Infrastructure.Extensions;

public static class StorageServiceExtensions
{
    public static IServiceCollection AddStorageInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var minioSection = configuration.GetSection("Minio");
        var endpoint = minioSection["Endpoint"] ?? configuration["MINIO_ENDPOINT"] ?? string.Empty;
        var secure = bool.TryParse(minioSection["Secure"] ?? configuration["MINIO_SECURE"], out bool sec) && sec;

        services.Configure<MinioSettings>(options =>
        {
            options.Endpoint = endpoint;
            options.AccessKey = minioSection["AccessKey"] ?? configuration["MINIO_ROOT_USER"] ?? string.Empty;
            options.SecretKey = minioSection["SecretKey"] ?? configuration["MINIO_ROOT_PASSWORD"] ?? string.Empty;
            options.PublicEndpoint = minioSection["PublicEndpoint"] ?? configuration["MINIO_PUBLIC_ENDPOINT"] ?? string.Empty;
            options.Secure = secure;
        });

        services.AddSingleton<IStorageService, MinioStorageService>();

        services.AddHttpClient("minio-health", client =>
        {
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                string scheme = secure ? "https" : "http";
                client.BaseAddress = new Uri($"{scheme}://{endpoint}");
            }

            client.Timeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }
}

