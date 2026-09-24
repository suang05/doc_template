namespace SmkDoc.Infrastructure.Storage;

public class MinioSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool Secure { get; set; } = false;
    public string PublicEndpoint { get; set; } = string.Empty;
}
