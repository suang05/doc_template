using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Integration.DataConnections;
using SmkDoc.Api.Controllers.Integration;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Application.Modules.Integration.DataConnections.Queries.GetDataConnectionById;
using SmkDoc.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class DataConnectionsControllerTests
{
    private readonly IUseCase<ListDataConnectionsQuery, List<DataConnectionDto>> _listUseCase = Substitute.For<IUseCase<ListDataConnectionsQuery, List<DataConnectionDto>>>();
    private readonly IUseCase<GetDataConnectionByIdQuery, DataConnectionDto?> _getByIdUseCase = Substitute.For<IUseCase<GetDataConnectionByIdQuery, DataConnectionDto?>>();
    private readonly IUseCase<CreateDataConnectionCommand, DataConnectionDto> _createUseCase = Substitute.For<IUseCase<CreateDataConnectionCommand, DataConnectionDto>>();
    private readonly IUseCase<UpdateDataConnectionCommand, DataConnectionDto?> _updateUseCase = Substitute.For<IUseCase<UpdateDataConnectionCommand, DataConnectionDto?>>();
    private readonly IUseCase<DeleteDataConnectionCommand, bool> _deleteUseCase = Substitute.For<IUseCase<DeleteDataConnectionCommand, bool>>();
    private readonly IUseCase<TestDataConnectionCommand, TestDataConnectionResultDto> _testUseCase = Substitute.For<IUseCase<TestDataConnectionCommand, TestDataConnectionResultDto>>();

    private DataConnectionsController CreateSut()
    {
        var controller = new DataConnectionsController(
            _listUseCase,
            _getByIdUseCase,
            _createUseCase,
            _updateUseCase,
            _deleteUseCase,
            _testUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    [Fact]
    public void Controller_ShouldBeDecoratedWithEnterpriseRbacPolicyAndRateLimiting()
    {
        var type = typeof(DataConnectionsController);

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Policy.Should().Be(SmkDocPolicies.CanManageDataConnections);

        var routeAttr = type.GetCustomAttribute<RouteAttribute>();
        routeAttr.Should().NotBeNull();
        routeAttr!.Template.Should().Be("api/v1/data-connections");

        var testMethod = type.GetMethod(nameof(DataConnectionsController.TestConnection));
        var rateLimitAttr = testMethod!.GetCustomAttribute<EnableRateLimitingAttribute>();
        rateLimitAttr.Should().NotBeNull();
        rateLimitAttr!.PolicyName.Should().Be("test-conn");
    }

    [Fact]
    public async Task GetAll_ShouldReturnConnectionsList()
    {
        // Arrange
        var sut = CreateSut();
        var dtos = new List<DataConnectionDto>
        {
            new(Guid.NewGuid(), "ERP DB", "PostgreSQL", DateTimeOffset.UtcNow, null)
        };
        _listUseCase.ExecuteAsync(Arg.Any<ListDataConnectionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(dtos);

        // Act
        var result = await sut.GetAll(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<DataConnectionDto>>>().Subject;
        body.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_WhenFound_ShouldReturnConnection()
    {
        // Arrange
        var sut = CreateSut();
        var id = Guid.NewGuid();
        var dto = new DataConnectionDto(id, "ERP DB", "PostgreSQL", DateTimeOffset.UtcNow, null);
        _getByIdUseCase.ExecuteAsync(Arg.Is<GetDataConnectionByIdQuery>(q => q.Id == id), Arg.Any<CancellationToken>())
            .Returns(dto);

        // Act
        var result = await sut.GetById(id, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<DataConnectionDto>>().Subject;
        body.Data.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ShouldReturn404()
    {
        // Arrange
        var sut = CreateSut();
        var id = Guid.NewGuid();
        _getByIdUseCase.ExecuteAsync(Arg.Is<GetDataConnectionByIdQuery>(q => q.Id == id), Arg.Any<CancellationToken>())
            .Returns((DataConnectionDto?)null);

        // Act
        var result = await sut.GetById(id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_ShouldReturn201Created()
    {
        // Arrange
        var sut = CreateSut();
        var id = Guid.NewGuid();
        var dto = new DataConnectionDto(id, "New Connection", "PostgreSQL", DateTimeOffset.UtcNow, null);
        _createUseCase.ExecuteAsync(Arg.Any<CreateDataConnectionCommand>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        var request = new CreateDataConnectionRequest("New Connection", "PostgreSQL", "Host=db;Database=erp");

        // Act
        var result = await sut.Create(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = createdResult.Value.Should().BeOfType<ApiResponse<DataConnectionDto>>().Subject;
        body.Data.Id.Should().Be(id);
    }

    [Fact]
    public async Task TestConnection_WhenReachable_ShouldReturnSemantic200OkWithSuccessTrue()
    {
        // Arrange
        var sut = CreateSut();
        _testUseCase.ExecuteAsync(Arg.Any<TestDataConnectionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new TestDataConnectionResultDto(true, "Connection successful."));

        var request = new TestDataConnectionRequest("PostgreSQL", "Host=db.prod;Database=erp");

        // Act
        var result = await sut.TestConnection(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var body = okResult.Value.Should().BeOfType<ApiResponse<TestDataConnectionResultDto>>().Subject;
        body.Data.Success.Should().BeTrue();
        body.Data.Message.Should().Be("Connection successful.");
    }

    [Fact]
    public async Task TestConnection_WhenUnreachable_ShouldReturnSemantic200OkWithSuccessFalse()
    {
        // Arrange
        var sut = CreateSut();
        _testUseCase.ExecuteAsync(Arg.Any<TestDataConnectionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new TestDataConnectionResultDto(false, "Connection failed."));

        var request = new TestDataConnectionRequest("PostgreSQL", "Host=unreachable.prod;Database=erp");

        // Act
        var result = await sut.TestConnection(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var body = okResult.Value.Should().BeOfType<ApiResponse<TestDataConnectionResultDto>>().Subject;
        body.Data.Success.Should().BeFalse();
        body.Data.Message.Should().Be("Connection failed.");
    }

    [Fact]
    public async Task TestConnection_WhenSsrfForbidden_ShouldBubbleForbiddenException()
    {
        // Arrange
        var sut = CreateSut();
        _testUseCase.ExecuteAsync(Arg.Any<TestDataConnectionCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ForbiddenException("Connecting to loopback host 'localhost' is prohibited."));

        var request = new TestDataConnectionRequest("PostgreSQL", "Host=localhost;Database=erp");

        // Act
        var act = () => sut.TestConnection(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*loopback*");
    }
}

