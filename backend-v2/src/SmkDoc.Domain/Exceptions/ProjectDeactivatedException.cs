namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when an operation is attempted within a deactivated project.
/// </summary>
public sealed class ProjectDeactivatedException(Guid projectId)
    : BusinessRuleViolationException($"Project '{projectId}' is deactivated.", "PROJECT_DEACTIVATED")
{
    public Guid ProjectId { get; } = projectId;
}
