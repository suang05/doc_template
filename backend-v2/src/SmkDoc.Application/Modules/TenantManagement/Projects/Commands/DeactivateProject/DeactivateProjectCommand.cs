namespace SmkDoc.Application.Modules.TenantManagement.Projects.Commands.DeactivateProject;

public sealed record DeactivateProjectCommand(Guid ProjectId, long ExpectedVersion = 1);
