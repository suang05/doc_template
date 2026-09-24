namespace SmkDoc.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? ApiKeyId { get; }
    Guid? ProjectId { get; }
    string IdentityName { get; } // Returns User's Email or ApiKey's Name for logging
    bool IsAuthenticated { get; }
}
