using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Thrown when connection string decryption fails for a data connection.
/// Maps to HTTP 500 Internal Server Error at the presentation layer.
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
