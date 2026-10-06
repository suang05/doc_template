using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class DataConnectionTests
{
    private readonly DateTimeOffset _initialTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var conn = DataConnection.Create(ConnectionName.Create("Warehouse PostgreSQL"), DatabaseProvider.PostgreSQL, "enc_pg_secret", _initialTime);

        conn.Id.Should().NotBe(Guid.Empty);
        conn.Name.Value.Should().Be("Warehouse PostgreSQL");
        conn.Provider.Should().Be(DatabaseProvider.PostgreSQL);
        conn.EncryptedConnectionString.Should().Be("enc_pg_secret");
        conn.CreatedAt.Should().Be(_initialTime);
        conn.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyConnectionString_ThrowsDomainValidationException()
    {
        var act = () => DataConnection.Create(ConnectionName.Create("Warehouse"), DatabaseProvider.PostgreSQL, "", _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*EncryptedConnectionString cannot be empty*");
    }

    [Fact]
    public void UpdateConnection_MutatesPropertiesAndSetsAuditTimestamp()
    {
        var conn = DataConnection.Create(ConnectionName.Create("Old DB"), DatabaseProvider.PostgreSQL, "enc_old", _initialTime);
        var updateTime = _initialTime.AddHours(3);

        conn.UpdateConnection(ConnectionName.Create("New DB"), DatabaseProvider.SqlServer, "enc_new", updateTime);

        conn.Name.Value.Should().Be("New DB");
        conn.Provider.Should().Be(DatabaseProvider.SqlServer);
        conn.EncryptedConnectionString.Should().Be("enc_new");
        conn.UpdatedAt.Should().Be(updateTime);
    }
}
