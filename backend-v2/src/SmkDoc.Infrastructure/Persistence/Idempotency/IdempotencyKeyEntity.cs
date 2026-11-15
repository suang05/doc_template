namespace SmkDoc.Infrastructure.Persistence.Idempotency;

/// <summary>
/// EF Core entity mapping for durable PostgreSQL idempotency records (<c>idempotency_keys</c>).
/// </summary>
public sealed class IdempotencyKeyEntity
{
    public Guid Id { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid TenantScopeId { get; init; }
    public string CredentialId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public string Status { get; set; } = "in_progress";
    public int? StatusCode { get; set; }
    public string? ResponseJson { get; set; }
    public string? ResponseHeadersJson { get; set; }
    public Guid? ResultResourceId { get; set; }
    public DateTimeOffset LockedUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
