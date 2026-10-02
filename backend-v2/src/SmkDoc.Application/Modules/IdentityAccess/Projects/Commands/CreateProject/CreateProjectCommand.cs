namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;

public record CreateProjectCommand(
    Guid UserId,
    string Name,
    string Slug
);
