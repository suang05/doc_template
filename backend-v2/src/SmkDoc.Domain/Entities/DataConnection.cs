using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing an encrypted database connection configuration.
/// </summary>
public sealed class DataConnection : BaseEntity
{
    public ConnectionName Name { get; private set; } = null!;
    public DatabaseProvider Provider { get; private set; } = DatabaseProvider.PostgreSQL;
    public string EncryptedConnectionString { get; private set; } = string.Empty;

    // For EF Core materialization only
    private DataConnection() { }

    internal DataConnection(
        Guid? id,
        ConnectionName name,
        DatabaseProvider provider,
        string encryptedConnectionString,
        DateTimeOffset now)
        : base(id, createdAt: now)
    {
        Name = Guard.NotNull(name, nameof(Name));
        Provider = provider ?? DatabaseProvider.PostgreSQL;
        EncryptedConnectionString = Guard.NotBlank(encryptedConnectionString, nameof(EncryptedConnectionString));
    }

    public static DataConnection Create(
        ConnectionName name,
        DatabaseProvider provider,
        string encryptedConnectionString,
        DateTimeOffset now) =>
        new(null, name, provider, encryptedConnectionString, now);

    public void UpdateConnection(
        ConnectionName name,
        DatabaseProvider provider,
        string encryptedConnectionString,
        DateTimeOffset now)
    {
        Name = Guard.NotNull(name, nameof(Name));
        Provider = provider ?? DatabaseProvider.PostgreSQL;
        EncryptedConnectionString = Guard.NotBlank(encryptedConnectionString, nameof(EncryptedConnectionString));
        SetUpdated(now);
    }
}
