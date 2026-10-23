using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public sealed class DataConnectionTests
{
    private readonly DateTimeOffset _initialTime = TestConstants.BaselineTime;
    private readonly ProjectId _projectId = ProjectId.New();

    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var conn = DataConnection.Create(ConnectionName.Create("Warehouse PostgreSQL"), DatabaseProvider.PostgreSQL, "enc_pg_secret", _initialTime, _projectId);

        conn.Id.Value.Should().NotBe(Guid.Empty);
        conn.ProjectId.Should().Be(_projectId);
        conn.Name.Value.Should().Be("Warehouse PostgreSQL");
        conn.Provider.Should().Be(DatabaseProvider.PostgreSQL);
        conn.EncryptedConnectionString.Should().Be("enc_pg_secret");
        conn.CreatedAt.Should().Be(_initialTime);
        conn.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var act = () => DataConnection.Create(ConnectionName.Create("Warehouse"), DatabaseProvider.PostgreSQL, "enc_pg_secret", _initialTime, ProjectId.Empty);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrWhitespaceConnectionString_ThrowsDomainValidationException(string invalidConn)
    {
        var act = () => DataConnection.Create(ConnectionName.Create("Warehouse"), DatabaseProvider.PostgreSQL, invalidConn, _initialTime, _projectId);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*EncryptedConnectionString cannot be empty*");
    }

    [Fact]
    public void UpdateConnection_MutatesPropertiesAndSetsAuditTimestamp()
    {
        var conn = DataConnection.Create(ConnectionName.Create("Old DB"), DatabaseProvider.PostgreSQL, "enc_old", _initialTime, _projectId);
        var updateTime = _initialTime.AddHours(3);

        conn.UpdateConnection(ConnectionName.Create("New DB"), DatabaseProvider.SqlServer, "enc_new", updateTime);

        conn.Name.Value.Should().Be("New DB");
        conn.Provider.Should().Be(DatabaseProvider.SqlServer);
        conn.EncryptedConnectionString.Should().Be("enc_new");
        conn.UpdatedAt.Should().Be(updateTime);
    }
}
