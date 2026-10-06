namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when an operation is attempted on or by a deactivated user.
/// </summary>
public sealed class UserDeactivatedException(Guid userId)
    : BusinessRuleViolationException($"User '{userId}' is deactivated.", "USER_DEACTIVATED")
{
    public Guid UserId { get; } = userId;
}
