using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Fixtures;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets;

public class DatasetUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

#pragma warning disable CS0618
    private DatasetUseCase CreateSut() =>
        new(_fixture.DatasetRepo.Object, _fixture.ConnectionRepo.Object, _fixture.UnitOfWork.Object);
#pragma warning restore CS0618

    // ── GetAllAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllDatasetsWithConnectionName()
    {
        var connId  = Guid.NewGuid();
        var conn    = new DataConnection("ProdDB", "PostgreSQL", "enc_prod", id: connId);
        var dataset = new Dataset("Orders", null, connId, "SELECT * FROM orders", 0);

        _fixture.DatasetRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<Dataset> { dataset });
        _fixture.ConnectionRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<DataConnection> { conn });

        var result = await CreateSut().GetAllAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Orders");
        result[0].DataConnectionName.Should().Be("ProdDB");
    }

    // ── CreateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ShouldPersistDatasetAndReturnDto()
    {
        var connId = Guid.NewGuid();
        var conn   = new DataConnection("DB1", "SqlServer", "enc_db1", id: connId);
        var dto    = new CreateDatasetDto
        {
            Name             = "InvoiceSet",
            DataConnectionId = connId,
            SqlQuery         = "SELECT total FROM invoices WHERE ref = @ref",
            CacheSeconds     = 60,
        };

        _fixture.ConnectionRepo.Setup(r => r.GetByIdAsync(connId, It.IsAny<CancellationToken>())).ReturnsAsync(conn);
        _fixture.DatasetRepo.Setup(r => r.AddAsync(It.IsAny<Dataset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(dto);

        result.Name.Should().Be("InvoiceSet");
        result.CacheSeconds.Should().Be(60);
        result.DataConnectionName.Should().Be("DB1");
        _fixture.DatasetRepo.Verify(r => r.AddAsync(It.Is<Dataset>(d =>
            d.Name             == "InvoiceSet" &&
            d.DataConnectionId == connId        &&
            d.CacheSeconds     == 60), It.IsAny<CancellationToken>()), Times.Once);
        _fixture.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenConnectionNotFound_ShouldThrow()
    {
        _fixture.ConnectionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((DataConnection?)null);

        Func<Task> act = () => CreateSut().CreateAsync(new CreateDatasetDto
        {
            Name             = "X",
            DataConnectionId = Guid.NewGuid(),
            SqlQuery         = "SELECT 1",
        });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── DeleteAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenExists_ShouldReturnTrue()
    {
        var entity = new Dataset("DS", null, Guid.NewGuid(), "SELECT 1", 0);
        _fixture.DatasetRepo.Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var result = await CreateSut().DeleteAsync(entity.Id);

        result.Should().BeTrue();
        _fixture.DatasetRepo.Verify(r => r.Remove(entity), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ShouldReturnFalse()
    {
        _fixture.DatasetRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Dataset?)null);

        var result = await CreateSut().DeleteAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }
}
