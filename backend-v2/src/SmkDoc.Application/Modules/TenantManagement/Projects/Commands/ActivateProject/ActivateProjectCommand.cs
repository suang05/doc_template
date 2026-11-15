namespace SmkDoc.Application.Modules.TenantManagement.Projects.Commands.ActivateProject;

public sealed record ActivateProjectCommand(Guid ProjectId, long ExpectedVersion = 1);
