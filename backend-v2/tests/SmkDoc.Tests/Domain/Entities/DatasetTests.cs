using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public sealed class DatasetTests
{
    private readonly DataConnectionId _connectionId = DataConnectionId.New();
    private readonly DateTimeOffset _initialTime = TestConstants.BaselineTime;
    private readonly ProjectId _projectId = ProjectId.New();

    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var dataset = Dataset.Create(
            DatasetName.Create("Monthly Invoices"),
            "Fetches invoices for reporting",
            _connectionId,
            SqlQuery.Create("SELECT * FROM invoices WHERE month = @month"),
            60,
            _initialTime,
            _projectId);

        dataset.Id.Value.Should().NotBe(Guid.Empty);
        dataset.ProjectId.Should().Be(_projectId);
        dataset.Name.Value.Should().Be("Monthly Invoices");
        dataset.Description.Should().Be("Fetches invoices for reporting");
        dataset.DataConnectionId.Should().Be(_connectionId);
        dataset.SqlQuery.Value.Should().Be("SELECT * FROM invoices WHERE month = @month");
        dataset.CacheSeconds.Should().Be(60);
        dataset.CreatedAt.Should().Be(_initialTime);
        dataset.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var act = () => Dataset.Create(DatasetName.Create("Orders"), null, _connectionId, SqlQuery.Create("SELECT 1"), 0, _initialTime, ProjectId.Empty);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyConnectionId_ThrowsDomainValidationException()
    {
        var act = () => Dataset.Create(DatasetName.Create("Orders"), null, DataConnectionId.Empty, SqlQuery.Create("SELECT 1"), 0, _initialTime, _projectId);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*DataConnectionId cannot be empty*");
    }

    [Fact]
    public void Create_WithNullSqlQuery_ThrowsDomainValidationException()
    {
        var act = () => Dataset.Create(DatasetName.Create("Orders"), null, _connectionId, null!, 0, _initialTime, _projectId);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*SqlQuery is required*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SqlQuery_WithEmptyOrWhitespace_ThrowsDomainValidationException(string invalidSql)
    {
        var act = () => SqlQuery.Create(invalidSql);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*SQL query cannot be empty*");
    }

    [Fact]
    public void Create_WithNegativeCacheSeconds_ThrowsDomainValidationException()
    {
        var act = () => Dataset.Create(DatasetName.Create("Orders"), null, _connectionId, SqlQuery.Create("SELECT 1"), -5, _initialTime, _projectId);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*CacheSeconds cannot be negative*");
    }

    [Fact]
    public void UpdateDetails_MutatesFieldsAndSetsAuditTimestamp()
    {
        var dataset = Dataset.Create(DatasetName.Create("Old Name"), "Old Desc", _connectionId, SqlQuery.Create("SELECT 1"), 0, _initialTime, _projectId);
        var updateTime = _initialTime.AddHours(1);
        var newConnId = DataConnectionId.New();

        dataset.UpdateDetails(DatasetName.Create("New Name"), "New Desc", newConnId, SqlQuery.Create("SELECT 2"), 120, updateTime);

        dataset.Name.Value.Should().Be("New Name");
        dataset.Description.Should().Be("New Desc");
        dataset.DataConnectionId.Should().Be(newConnId);
        dataset.SqlQuery.Value.Should().Be("SELECT 2");
        dataset.CacheSeconds.Should().Be(120);
        dataset.UpdatedAt.Should().Be(updateTime);
    }
}
