namespace SmkDoc.Application.Common.Interfaces;

public interface IExecutionContext
{
    Guid? ProjectId { get; }
    Guid? UserId { get; }
    Guid? ApiKeyId { get; }
    string? CallerApp { get; }
    string? ClientIp { get; }
    string? UserAgent { get; }
}

