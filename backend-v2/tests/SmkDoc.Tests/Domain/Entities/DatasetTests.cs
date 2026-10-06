using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class DatasetTests
{
    private readonly Guid _connectionId = Guid.NewGuid();
    private readonly DateTimeOffset _initialTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var dataset = Dataset.Create(
            DatasetName.Create("Monthly Invoices"),
            "Fetches invoices for reporting",
            _connectionId,
            "SELECT * FROM invoices WHERE month = @month",
            60,
            _initialTime);

        dataset.Id.Should().NotBe(Guid.Empty);
        dataset.Name.Value.Should().Be("Monthly Invoices");
        dataset.Description.Should().Be("Fetches invoices for reporting");
        dataset.DataConnectionId.Should().Be(_connectionId);
        dataset.SqlQuery.Should().Be("SELECT * FROM invoices WHERE month = @month");
        dataset.CacheSeconds.Should().Be(60);
        dataset.CreatedAt.Should().Be(_initialTime);
        dataset.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyConnectionId_ThrowsDomainValidationException()
    {
        var act = () => Dataset.Create(DatasetName.Create("Orders"), null, Guid.Empty, "SELECT 1", 0, _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*DataConnectionId cannot be empty*");
    }

    [Fact]
    public void Create_WithNegativeCacheSeconds_ThrowsDomainValidationException()
    {
        var act = () => Dataset.Create(DatasetName.Create("Orders"), null, _connectionId, "SELECT 1", -5, _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*CacheSeconds cannot be negative*");
    }

    [Fact]
    public void UpdateDetails_MutatesFieldsAndSetsAuditTimestamp()
    {
        var dataset = Dataset.Create(DatasetName.Create("Old Name"), "Old Desc", _connectionId, "SELECT 1", 0, _initialTime);
        var updateTime = _initialTime.AddHours(1);
        var newConnId = Guid.NewGuid();

        dataset.UpdateDetails(DatasetName.Create("New Name"), "New Desc", newConnId, "SELECT 2", 120, updateTime);

        dataset.Name.Value.Should().Be("New Name");
        dataset.Description.Should().Be("New Desc");
        dataset.DataConnectionId.Should().Be(newConnId);
        dataset.SqlQuery.Should().Be("SELECT 2");
        dataset.CacheSeconds.Should().Be(120);
        dataset.UpdatedAt.Should().Be(updateTime);
    }
}
