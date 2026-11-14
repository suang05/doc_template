namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Thrown at Tier 1 perimeter when a Data Plane request omits the required <c>Project-Id</c> header.
/// Maps to HTTP 400 Bad Request with <see cref="ErrorCodes.ProjectContextRequired"/>.
/// </summary>
public sealed class ProjectContextRequiredException : DomainException
{
    public ProjectContextRequiredException(string? message = null, Exception? innerException = null)
        : base(
            message ?? "The 'Project-Id' header is required for project-scoped requests.",
            ErrorCodes.ProjectContextRequired,
            innerException)
    {
    }
}
