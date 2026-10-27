using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Infrastructure.Contexts;

public class ExecutionContextImpl(ICurrentProjectContext? projectContext = null) : IExecutionContext
{
    private readonly ICurrentProjectContext? _projectContext = projectContext;

    public Guid? ProjectId => _projectContext?.IsResolved == true ? _projectContext.ProjectId.Value : null;
    public Guid? UserId { get; set; }
    public Guid? ApiKeyId { get; set; }
    public ApiKeyScope? Scope { get; set; }
    public string? CallerApp { get; set; }
    public string? ClientIp { get; set; }
    public string? UserAgent { get; set; }
}

