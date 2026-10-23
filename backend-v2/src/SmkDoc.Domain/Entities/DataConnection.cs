using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing an encrypted database connection configuration.
/// </summary>
public sealed class DataConnection : BaseEntity<DataConnectionId>, IMustHaveProject
{
    public ProjectId ProjectId { get; private set; }
    public ConnectionName Name { get; private set; } = null!;
    public DatabaseProvider Provider { get; private set; } = DatabaseProvider.PostgreSQL;
    public EncryptedConnectionString EncryptedConnectionString { get; private set; } = null!;

    // For EF Core materialization only
    private DataConnection() { }

    internal DataConnection(
        DataConnectionId? id,
        ConnectionName name,
        DatabaseProvider provider,
        EncryptedConnectionString encryptedConnectionString,
        DateTimeOffset now,
        ProjectId projectId)
        : base(id ?? DataConnectionId.New(), createdAt: now)
    {
        Name = Guard.NotNull(name, nameof(Name));
        Provider = provider ?? DatabaseProvider.PostgreSQL;
        EncryptedConnectionString = Guard.NotNull(encryptedConnectionString, nameof(EncryptedConnectionString));
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
    }

    public static DataConnection Create(
        ConnectionName name,
        DatabaseProvider provider,
        EncryptedConnectionString encryptedConnectionString,
        DateTimeOffset now,
        ProjectId projectId) =>
        new(null, name, provider, encryptedConnectionString, now, projectId);

    public void UpdateConnection(
        ConnectionName name,
        DatabaseProvider provider,
        EncryptedConnectionString encryptedConnectionString,
        DateTimeOffset now)
    {
        Name = Guard.NotNull(name, nameof(Name));
        Provider = provider ?? DatabaseProvider.PostgreSQL;
        EncryptedConnectionString = Guard.NotNull(encryptedConnectionString, nameof(EncryptedConnectionString));
        SetUpdated(now);
    }
}
