namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when connection string decryption fails for a data connection.
/// </summary>
public sealed class DataConnectionDecryptionException : DomainException
{
    public Guid ConnectionId { get; }
    public string ConnectionName { get; }

    public DataConnectionDecryptionException(Guid connectionId, string connectionName, Exception? innerException = null)
        : base($"Failed to decrypt connection string for data connection '{connectionName}' ({connectionId}).", "DATA_CONNECTION_DECRYPTION_FAILED", innerException)
    {
        ConnectionId = connectionId;
        ConnectionName = connectionName;
    }
}
