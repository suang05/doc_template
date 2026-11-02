namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;

public sealed record CreateProjectCommand(
    Guid UserId,
    string Name,
    string Slug
);
