using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Api.Common.Context;

public class ExecutionContextImpl : IExecutionContext
{
    public Guid? ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ApiKeyId { get; set; }
    public string? CallerApp { get; set; }
    public string? ClientIp { get; set; }
    public string? UserAgent { get; set; }
}
