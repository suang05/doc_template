using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Common.Interfaces;

public interface IExecutionContext
{
    /// <summary>
    /// Legacy bridge property. Consume <see cref="ICurrentProjectContext.ProjectId"/> as the Single Source of Truth for tenant isolation.
    /// </summary>
    Guid? ProjectId { get; }
    Guid? UserId { get; }
    Guid? ApiKeyId { get; }
    ApiKeyScope? Scope { get; }
    string? CallerApp { get; }
    string? ClientIp { get; }
    string? UserAgent { get; }
}

