namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Thrown when the <c>Project-Id</c> header conflicts with the project bound to an API key credential.
/// Maps to HTTP 400 Bad Request with <see cref="ErrorCodes.ProjectContextMismatch"/>.
/// </summary>
public sealed class ProjectContextMismatchException : DomainException
{
    public ProjectContextMismatchException(string? message = null, Exception? innerException = null)
        : base(
            message ?? "The 'Project-Id' header does not match the project bound to the provided credential.",
            ErrorCodes.ProjectContextMismatch,
            innerException)
    {
    }
}
