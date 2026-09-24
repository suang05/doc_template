using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Api.Models;

public class ExecutionContextImpl : IExecutionContext
{
    public Guid? ApiKeyId { get; set; }
    public string? CallerApp { get; set; }
    public string? ClientIp { get; set; }
    public string? UserAgent { get; set; }
}
