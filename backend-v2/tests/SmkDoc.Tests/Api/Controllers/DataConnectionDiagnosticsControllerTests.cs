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
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class DataConnectionDiagnosticsControllerTests
{
    private readonly IUseCase<TestDataConnectionCommand, TestDataConnectionResultDto> _testUseCase = Substitute.For<IUseCase<TestDataConnectionCommand, TestDataConnectionResultDto>>();

    private DataConnectionDiagnosticsController CreateSut()
    {
        var controller = new DataConnectionDiagnosticsController(_testUseCase)
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
        var type = typeof(DataConnectionDiagnosticsController);

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Policy.Should().Be(SmkDocPolicies.CanManageDataConnections);

        var routeAttr = type.GetCustomAttribute<RouteAttribute>();
        routeAttr.Should().NotBeNull();
        routeAttr!.Template.Should().Be("api/v1/data-connections");

        var testMethod = type.GetMethod(nameof(DataConnectionDiagnosticsController.TestConnection));
        var rateLimitAttr = testMethod!.GetCustomAttribute<EnableRateLimitingAttribute>();
        rateLimitAttr.Should().NotBeNull();
        rateLimitAttr!.PolicyName.Should().Be("test-conn");
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

