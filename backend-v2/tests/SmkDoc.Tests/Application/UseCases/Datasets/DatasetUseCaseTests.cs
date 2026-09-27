using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.Datasets;
using SmkDoc.Application.UseCases.Datasets;
using SmkDoc.Domain.Entities;
using Xunit;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Tests.Application.UseCases.Datasets;

public class DatasetUseCaseTests
{
    private readonly Mock<IRepository<Dataset>> _datasetRepo = new();
    private readonly Mock<IRepository<DataConnection>> _connRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private DatasetUseCase CreateSut() =>
        new(_datasetRepo.Object, _connRepo.Object, _uow.Object);

    // ── GetAllAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllDatasetsWithConnectionName()
    {
        var connId  = Guid.NewGuid();
        var conn    = new DataConnection("ProdDB", "PostgreSQL", "") { Id = connId };
        var dataset = new Dataset("Orders", null, connId, "SELECT * FROM orders", 0) { Id = Guid.NewGuid() };

        _datasetRepo.Setup(r => r.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Dataset, bool>>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<Dataset> { dataset });
        _connRepo.Setup(r => r.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<DataConnection, bool>>>(), It.IsAny<CancellationToken>()))
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
        var conn   = new DataConnection("DB1", "SqlServer", "") { Id = connId };
        var dto    = new CreateDatasetDto
        {
            Name             = "InvoiceSet",
            DataConnectionId = connId,
            SqlQuery         = "SELECT total FROM invoices WHERE ref = @ref",
            CacheSeconds     = 60,
        };

        _connRepo.Setup(r => r.GetByIdAsync(connId, It.IsAny<CancellationToken>())).ReturnsAsync(conn);
        _datasetRepo.Setup(r => r.AddAsync(It.IsAny<Dataset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateSut().CreateAsync(dto);

        result.Name.Should().Be("InvoiceSet");
        result.CacheSeconds.Should().Be(60);
        result.DataConnectionName.Should().Be("DB1");
        _datasetRepo.Verify(r => r.AddAsync(It.Is<Dataset>(d =>
            d.Name             == "InvoiceSet" &&
            d.DataConnectionId == connId        &&
            d.CacheSeconds     == 60), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenConnectionNotFound_ShouldThrow()
    {
        _connRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((DataConnection?)null);

        var act = () => CreateSut().CreateAsync(new CreateDatasetDto
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
        var entity = new Dataset("DS", null, Guid.NewGuid(), "SELECT 1", 0) { Id = Guid.NewGuid() };
        _datasetRepo.Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateSut().DeleteAsync(entity.Id);

        result.Should().BeTrue();
        _datasetRepo.Verify(r => r.Remove(entity), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ShouldReturnFalse()
    {
        _datasetRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Dataset?)null);

        var result = await CreateSut().DeleteAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }
}
