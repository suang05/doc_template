using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Integration.Datasets;
using SmkDoc.Api.Controllers.Integration;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.GetDatasetById;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.ListDatasets;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class DatasetsControllerTests
{
    private readonly IUseCase<ListDatasetsQuery, List<DatasetResultDto>> _listUseCase = Substitute.For<IUseCase<ListDatasetsQuery, List<DatasetResultDto>>>();
    private readonly IUseCase<GetDatasetByIdQuery, DatasetResultDto> _getByIdUseCase = Substitute.For<IUseCase<GetDatasetByIdQuery, DatasetResultDto>>();
    private readonly IUseCase<CreateDatasetCommand, DatasetResultDto> _createUseCase = Substitute.For<IUseCase<CreateDatasetCommand, DatasetResultDto>>();
    private readonly IUseCase<UpdateDatasetCommand, DatasetResultDto> _updateUseCase = Substitute.For<IUseCase<UpdateDatasetCommand, DatasetResultDto>>();
    private readonly IUseCase<DeleteDatasetCommand, bool> _deleteUseCase = Substitute.For<IUseCase<DeleteDatasetCommand, bool>>();

    private DatasetsController CreateSut()
    {
        var controller = new DatasetsController(
            _listUseCase,
            _getByIdUseCase,
            _createUseCase,
            _updateUseCase,
            _deleteUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    [Fact]
    public void Controller_ShouldBeDecoratedWithDatasetManagementPolicy()
    {
        var type = typeof(DatasetsController);

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Policy.Should().Be(AuthorizationPolicies.CanManageDatasets);

        var routeAttr = type.GetCustomAttribute<RouteAttribute>();
        routeAttr.Should().NotBeNull();
        routeAttr!.Template.Should().Be("api/v1/datasets");
    }

    [Fact]
    public async Task GetAll_ShouldReturnDatasetsList()
    {
        // Arrange
        var sut = CreateSut();
        var dtos = new List<DatasetResultDto>
        {
            new(Guid.NewGuid(), "Invoices", "Monthly customer invoices", Guid.NewGuid(), "ERP DB", "SELECT * FROM invoices", 300, DateTimeOffset.UtcNow, null, "1111")
        };
        _listUseCase.ExecuteAsync(Arg.Any<ListDatasetsQuery>(), Arg.Any<CancellationToken>())
            .Returns(dtos);

        // Act
        var result = await sut.GetAll(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<DatasetResultDto>>>().Subject;
        body.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_WhenFound_ShouldReturnDataset()
    {
        // Arrange
        var sut = CreateSut();
        var id = Guid.NewGuid();
        var dto = new DatasetResultDto(id, "Invoices", "Monthly customer invoices", Guid.NewGuid(), "ERP DB", "SELECT * FROM invoices", 300, DateTimeOffset.UtcNow, null, "2222");
        _getByIdUseCase.ExecuteAsync(Arg.Is<GetDatasetByIdQuery>(q => q.Id == id), Arg.Any<CancellationToken>())
            .Returns(dto);

        // Act
        var result = await sut.GetById(id, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<DatasetResultDto>>().Subject;
        body.Data.Id.Should().Be(id);
    }

    [Fact]
    public async Task Create_ShouldReturn201CreatedAndBeDecoratedWithIdempotent()
    {
        // Arrange
        var sut = CreateSut();
        var id = Guid.NewGuid();
        var connId = Guid.NewGuid();
        var dto = new DatasetResultDto(id, "Invoices", "Monthly customer invoices", connId, "ERP DB", "SELECT * FROM invoices", 300, DateTimeOffset.UtcNow, null, "3333");
        _createUseCase.ExecuteAsync(Arg.Any<CreateDatasetCommand>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        var request = new CreateDatasetRequest("Invoices", "Monthly customer invoices", connId, "SELECT * FROM invoices", 300);

        // Act
        var result = await sut.Create(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = createdResult.Value.Should().BeOfType<ApiResponse<DatasetResultDto>>().Subject;
        body.Data.Id.Should().Be(id);

        var method = typeof(DatasetsController).GetMethod(nameof(DatasetsController.Create));
        method!.GetCustomAttribute<IdempotentAttribute>().Should().NotBeNull();
    }

    [Fact]
    public async Task Update_ShouldExecuteCommandAndReturnOk()
    {
        // Arrange
        var sut = CreateSut();
        var id = Guid.NewGuid();
        var connId = Guid.NewGuid();
        var dto = new DatasetResultDto(id, "Invoices", "Monthly customer invoices", connId, "ERP DB", "SELECT * FROM invoices", 300, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "4444");
        _updateUseCase.ExecuteAsync(Arg.Is<UpdateDatasetCommand>(c => c.Id == id), Arg.Any<CancellationToken>())
            .Returns(dto);

        var request = new UpdateDatasetRequest("Invoices", "Monthly customer invoices", connId, "SELECT * FROM invoices", 300);

        // Act
        var result = await sut.Update(id, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<DatasetResultDto>>().Subject;
        body.Data.Id.Should().Be(id);
    }

    [Fact]
    public async Task Delete_ShouldExecuteCommandAndReturnNoContent()
    {
        // Arrange
        var sut = CreateSut();
        var id = Guid.NewGuid();
        _deleteUseCase.ExecuteAsync(Arg.Is<DeleteDatasetCommand>(c => c.Id == id), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await sut.Delete(id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }
}

