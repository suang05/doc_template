namespace SmkDoc.Application.Modules.TenantManagement.Projects.Commands.UpdateProjectEgressCidrs;

public sealed record UpdateProjectEgressCidrsCommand(
    Guid ProjectId,
    IReadOnlyList<string> AllowedCidrs,
    long ExpectedVersion = 1);
