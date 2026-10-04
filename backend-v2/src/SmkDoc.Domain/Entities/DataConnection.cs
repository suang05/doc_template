using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing an encrypted database connection configuration.
/// </summary>
public class DataConnection : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Provider { get; private set; } = "PostgreSQL";
    public string EncryptedConnectionString { get; private set; } = string.Empty;

    // For EF Core materialization
    private DataConnection() { }

    public DataConnection(string name, string provider, string encryptedConnectionString, Guid? id = null)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Data connection name cannot be empty or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new DomainValidationException("Provider cannot be empty or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(encryptedConnectionString))
        {
            throw new DomainValidationException("Encrypted connection string cannot be empty or whitespace.");
        }

        Name = name.Trim();
        Provider = provider.Trim();
        EncryptedConnectionString = encryptedConnectionString;
    }

    public void UpdateConnection(string name, string provider, string encryptedConnectionString)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Data connection name cannot be empty or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new DomainValidationException("Provider cannot be empty or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(encryptedConnectionString))
        {
            throw new DomainValidationException("Encrypted connection string cannot be empty or whitespace.");
        }

        Name = name.Trim();
        Provider = provider.Trim();
        EncryptedConnectionString = encryptedConnectionString;
        SetUpdated();
    }
}
