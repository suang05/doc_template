namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when an external SQL dataset query fails during document data preparation.
/// </summary>
public sealed class DatasetExecutionException : DomainException
{
    public string Alias { get; }
    public string Provider { get; }

    public DatasetExecutionException(string alias, string provider, string message, Exception? innerException = null)
        : base($"Failed to execute dataset query for alias '{alias}' ({provider}): {message}", "DATASET_QUERY_FAILED", innerException)
    {
        Alias = alias;
        Provider = provider;
    }
}
