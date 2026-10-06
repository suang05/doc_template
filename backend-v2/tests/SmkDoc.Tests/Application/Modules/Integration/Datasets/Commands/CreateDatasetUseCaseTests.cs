using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Factories;
using SmkDoc.Tests.Common.Fixtures;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Commands;

public class CreateDatasetUseCaseTests
{
    private readonly IntegrationModuleTestFixture _fixture = new();

    private CreateDatasetUseCase CreateSut() =>
        new(_fixture.DatasetRepo.Object, _fixture.ConnectionRepo.Object, _fixture.UnitOfWork.Object);

    [Fact]
    public async Task ExecuteAsync_ShouldPersistDatasetAndReturnDto()
    {
        var connId = Guid.NewGuid();
        var conn = DataConnectionTestFactory.Create(connId, "DB1", DatabaseProvider.SqlServer, "enc_db1");
        var command = new CreateDatasetCommand("OrdersSet", "desc", connId, "SELECT * FROM orders", 60);

        _fixture.ConnectionRepo.Setup(r => r.GetByIdAsync(connId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conn);

        var result = await CreateSut().ExecuteAsync(command);

        result.Name.Should().Be("OrdersSet");
        result.DataConnectionName.Should().Be("DB1");
        result.CacheSeconds.Should().Be(60);

        _fixture.DatasetRepo.Verify(r => r.AddAsync(It.Is<Dataset>(d =>
            d.Name.Value == "OrdersSet" &&
            d.DataConnectionId == connId &&
            d.CacheSeconds == 60), It.IsAny<CancellationToken>()), Times.Once);
        _fixture.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionNotFound_ShouldThrowNotFoundException()
    {
        _fixture.ConnectionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataConnection?)null);

        var command = new CreateDatasetCommand("OrdersSet", null, Guid.NewGuid(), "SELECT 1");

        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
