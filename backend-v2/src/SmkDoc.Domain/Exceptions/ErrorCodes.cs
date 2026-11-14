namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Canonical machine-readable RFC 9457 <c>errorCode</c> constants shared across all layers.
/// </summary>
public static class ErrorCodes
{
    // Tier 1 / Authentication & Tenancy
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string ProjectContextRequired = "PROJECT_CONTEXT_REQUIRED";
    public const string ProjectContextMismatch = "PROJECT_CONTEXT_MISMATCH";
    public const string CrossTenantAccessDenied = "CROSS_TENANT_ACCESS_DENIED";
    public const string ProjectDeactivated = "PROJECT_DEACTIVATED";
    public const string InsufficientKeyScope = "INSUFFICIENT_KEY_SCOPE";
    public const string TenantContextNotResolved = "TENANT_CONTEXT_NOT_RESOLVED";

    // Tier 2 / Resource & Business Conflict
    public const string NotFound = "NOT_FOUND";
    public const string ResourceConflict = "RESOURCE_CONFLICT";
    public const string DuplicateSlug = "DUPLICATE_SLUG";
    public const string BusinessRuleViolation = "BUSINESS_RULE_VIOLATION";
    public const string DraftExpired = "DRAFT_EXPIRED";

    // Concurrency & Preconditions
    public const string PreconditionFailed = "PRECONDITION_FAILED";
    public const string PreconditionRequired = "PRECONDITION_REQUIRED";

    // Validation & Protocol
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string SchemaValidationFailed = "SCHEMA_VALIDATION_FAILED";
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string IdempotencyInProgress = "IDEMPOTENCY_IN_PROGRESS";
    public const string MalformedRequest = "MALFORMED_REQUEST";
    public const string RateLimited = "RATE_LIMITED";

    // External Integration & Rendering
    public const string EgressDenied = "EGRESS_DENIED";
    public const string ExternalServiceUnavailable = "EXTERNAL_SERVICE_UNAVAILABLE";
    public const string GatewayTimeout = "GATEWAY_TIMEOUT";
    public const string RenderFailed = "RENDER_FAILED";
    public const string DataConnectionDecryptionFailed = "DATA_CONNECTION_DECRYPTION_FAILED";
    public const string DatasetExecutionFailed = "DATASET_EXECUTION_FAILED";

    // Domain Subcodes
    public const string InactiveTemplate = "INACTIVE_TEMPLATE";
    public const string TemplateVersionNotFound = "TEMPLATE_VERSION_NOT_FOUND";

    // Fallback
    public const string InternalError = "INTERNAL_ERROR";
}
