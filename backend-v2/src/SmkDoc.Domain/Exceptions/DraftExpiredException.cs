namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when a draft has expired or does not exist in the temporary cache.
/// Maps to HTTP 410 Gone at the presentation layer.
/// </summary>
public sealed class DraftExpiredException : DomainException
{
    public string DraftId { get; }

    public DraftExpiredException(string draftId, Exception? innerException = null)
        : base($"Draft '{draftId}' has expired or does not exist. Please re-upload the file.", "DRAFT_EXPIRED", 410, innerException) 
    { 
        DraftId = draftId;
    }
}
