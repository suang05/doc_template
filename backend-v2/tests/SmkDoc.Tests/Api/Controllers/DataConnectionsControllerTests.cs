using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Integration.DataConnections;
using SmkDoc.Api.Controllers.Integration;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Application.Modules.Integration.DataConnections.Queries.GetDataConnectionById;
using SmkDoc.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class DataConnectionsControllerTests
{
    private readonly IUseCase<ListDataConnectionsQuery, List<DataConnectionResultDto>> _listUseCase = Substitute.For<IUseCase<ListDataConnectionsQuery, List<DataConnectionResultDto>>>();
    private readonly IUseCase<GetDataConnectionByIdQuery, DataConnectionResultDto> _getByIdUseCase = Substitute.For<IUseCase<GetDataConnectionByIdQuery, DataConnectionResultDto>>();
    private readonly IUseCase<CreateDataConnectionCommand, DataConnectionResultDto> _createUseCase = Substitute.For<IUseCase<CreateDataConnectionCommand, DataConnectionResultDto>>();
    private readonly IUseCase<UpdateDataConnectionCommand, DataConnectionResultDto> _updateUseCase = Substitute.For<IUseCase<UpdateDataConnectionCommand, DataConnectionResultDto>>();
    private readonly IUseCase<DeleteDataConnectionCommand, bool> _deleteUseCase = Substitute.For<IUseCase<DeleteDataConnectionCommand, bool>>();

    private DataConnectionsController CreateSut()
    {
        var controller = new DataConnectionsController(
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
    public void Controller_ShouldBeDecoratedWithEnterpriseRbacPolicy()
    {
        var type = typeof(DataConnectionsController);

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Policy.Should().Be(AuthorizationPolicies.CanManageDataConnections);

        var routeAttr = type.GetCustomAttribute<RouteAttribute>();
        routeAttr.Should().NotBeNull();
        routeAttr!.Template.Should().Be("api/v1/data-connections");
    }

    [Fact]
    public async Task GetAll_ShouldReturnConnectionsList()
    {
        // Arrange
        var sut = CreateSut();
        var dtos = new List<DataConnectionResultDto>
        {
            new(Guid.NewGuid(), "ERP DB", "PostgreSQL", DateTimeOffset.UtcNow, null, "12345")
        };
        _listUseCase.ExecuteAsync(Arg.Any<ListDataConnectionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(dtos);

        // Act
        var result = await sut.GetAll(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<DataConnectionResultDto>>>().Subject;
        body.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_WhenFound_ShouldReturnConnectionAndSetETag()
    {
        // Arrange
        var sut = CreateSut();
        var id = Guid.NewGuid();
        var dto = new DataConnectionResultDto(id, "ERP DB", "PostgreSQL", DateTimeOffset.UtcNow, null, "12345");
        _getByIdUseCase.ExecuteAsync(Arg.Is<GetDataConnectionByIdQuery>(q => q.Id == id), Arg.Any<CancellationToken>())
            .Returns(dto);

        // Act
        var result = await sut.GetById(id, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<DataConnectionResultDto>>().Subject;
        body.Data.Id.Should().Be(id);
        sut.Response.Headers.ETag.ToString().Should().Be("\"12345\"");
    }

    [Fact]
    public async Task GetById_WhenNotFound_ShouldBubbleNotFoundException()
    {
        // Arrange
        var sut = CreateSut();
        var id = Guid.NewGuid();
        _getByIdUseCase.ExecuteAsync(Arg.Is<GetDataConnectionByIdQuery>(q => q.Id == id), Arg.Any<CancellationToken>())
            .ThrowsAsync(new NotFoundException("DataConnection", id));

        // Act
        var act = () => sut.GetById(id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_ShouldReturn201CreatedAndBeDecoratedWithIdempotent()
    {
        // Arrange
        var sut = CreateSut();
        var id = Guid.NewGuid();
        var dto = new DataConnectionResultDto(id, "New Connection", "PostgreSQL", DateTimeOffset.UtcNow, null, "9999");
        _createUseCase.ExecuteAsync(Arg.Any<CreateDataConnectionCommand>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        var request = new CreateDataConnectionRequest("New Connection", "PostgreSQL", "Host=db;Database=erp");

        // Act
        var result = await sut.Create(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = createdResult.Value.Should().BeOfType<ApiResponse<DataConnectionResultDto>>().Subject;
        body.Data.Id.Should().Be(id);

        var method = typeof(DataConnectionsController).GetMethod(nameof(DataConnectionsController.Create));
        method!.GetCustomAttribute<IdempotentAttribute>().Should().NotBeNull();
    }

    [Fact]
    public async Task Update_ShouldForwardIfMatchAndSetETagHeader()
    {
        // Arrange
        var sut = CreateSut();
        var id = Guid.NewGuid();
        var dto = new DataConnectionResultDto(id, "Updated Connection", "PostgreSQL", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "8888");
        _updateUseCase.ExecuteAsync(Arg.Is<UpdateDataConnectionCommand>(c => c.Id == id && c.IfMatch == "\"7777\""), Arg.Any<CancellationToken>())
            .Returns(dto);

        var request = new UpdateDataConnectionRequest("Updated Connection", "PostgreSQL", "Host=db;Database=erp");

        // Act
        var result = await sut.Update(id, request, "\"7777\"", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<DataConnectionResultDto>>().Subject;
        body.Data.Id.Should().Be(id);
        sut.Response.Headers.ETag.ToString().Should().Be("\"8888\"");
    }

    [Fact]
    public async Task Delete_ShouldForwardIfMatch()
    {
        // Arrange
        var sut = CreateSut();
        var id = Guid.NewGuid();
        _deleteUseCase.ExecuteAsync(Arg.Is<DeleteDataConnectionCommand>(c => c.Id == id && c.IfMatch == "\"7777\""), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await sut.Delete(id, "\"7777\"", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }
}
