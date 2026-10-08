using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Common.Interfaces;

public interface IExecutionContext
{
    Guid? ProjectId { get; }
    Guid? UserId { get; }
    Guid? ApiKeyId { get; }
    ApiKeyScope? Scope { get; }
    string? CallerApp { get; }
    string? ClientIp { get; }
    string? UserAgent { get; }
}

