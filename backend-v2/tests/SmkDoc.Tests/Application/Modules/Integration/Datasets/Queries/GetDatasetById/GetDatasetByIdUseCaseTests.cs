using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.GetDatasetById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets.Queries.GetDatasetById;

public sealed class GetDatasetByIdUseCaseTests
{
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    public GetDatasetByIdUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(ProjectId.New());
    }

    private GetDatasetByIdUseCase CreateSut() => new(
        _datasetRepo,
        _connectionRepo,
        _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenProjectUnresolved_ThrowsForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);

        // Act
        var act = () => CreateSut().ExecuteAsync(new GetDatasetByIdQuery(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        await _datasetRepo.DidNotReceive().GetByIdAsync(Arg.Any<DatasetId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenFound_ReturnsEnrichedDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var connId = Guid.NewGuid();
        DataConnection conn = DataConnectionBuilder.Postgres(name: "AnalyticsDB", connectionString: "enc_analytics", id: connId);
        Dataset dataset = new DatasetBuilder()
            .WithId(id)
            .WithName("SalesMetrics")
            .WithDescription("desc")
            .WithDataConnectionId(connId)
            .WithSqlQuery("SELECT * FROM sales")
            .WithCacheSeconds(300);

        _datasetRepo.GetByIdAsync(new DatasetId(id), Arg.Any<CancellationToken>())
            .Returns(dataset);

        _connectionRepo.GetByIdAsync(new DataConnectionId(connId), Arg.Any<CancellationToken>())
            .Returns(conn);

        // Act
        DatasetResultDto result = await CreateSut().ExecuteAsync(new GetDatasetByIdQuery(id));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(id);
        result.Name.Should().Be("SalesMetrics");
        result.DataConnectionName.Should().Be("AnalyticsDB");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _datasetRepo.GetByIdAsync(new DatasetId(id), Arg.Any<CancellationToken>())
            .Returns((Dataset?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new GetDatasetByIdQuery(id));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
