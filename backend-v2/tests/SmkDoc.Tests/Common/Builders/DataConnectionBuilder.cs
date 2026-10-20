using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Builders;

public class DataConnectionBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Default Connection";
    private DatabaseProvider _provider = DatabaseProvider.PostgreSQL;
    private string _encryptedConnectionString = "Host=localhost;Database=test";
    private DateTimeOffset _now = TestConstants.BaselineTime;

    public static DataConnectionBuilder AConnection() => new();

    public static DataConnection Default() => AConnection().Build();

    public static DataConnection Postgres(string name = "Postgres Connection", string connectionString = "Host=localhost;Database=test", Guid? id = null) =>
        new DataConnectionBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithName(name)
            .WithProvider(DatabaseProvider.PostgreSQL)
            .WithConnectionString(connectionString)
            .Build();

    public static DataConnection SqlServer(string name = "SqlServer Connection", string connectionString = "Server=localhost;Database=test", Guid? id = null) =>
        new DataConnectionBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithName(name)
            .WithProvider(DatabaseProvider.SqlServer)
            .WithConnectionString(connectionString)
            .Build();


    public DataConnectionBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public DataConnectionBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public DataConnectionBuilder WithProvider(DatabaseProvider provider)
    {
        _provider = provider;
        return this;
    }

    public DataConnectionBuilder WithConnectionString(string encryptedConnectionString)
    {
        _encryptedConnectionString = encryptedConnectionString;
        return this;
    }

    public DataConnectionBuilder WithTime(DateTimeOffset now)
    {
        _now = now;
        return this;
    }

    public DataConnection Build()
    {
        return new DataConnection(
            _id,
            ConnectionName.Create(_name),
            _provider,
            _encryptedConnectionString,
            _now);
    }
}
