using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Integration.Datasets;
using SmkDoc.Api.Controllers.Integration;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.DryRunDataset;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class DatasetDiagnosticsControllerTests
{
    private readonly IUseCase<DryRunDatasetCommand, DryRunDatasetResultDto> _dryRunUseCase =
        Substitute.For<IUseCase<DryRunDatasetCommand, DryRunDatasetResultDto>>();

    private DatasetDiagnosticsController CreateSut()
    {
        return new DatasetDiagnosticsController(_dryRunUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void Controller_ShouldBeDecoratedWithDatasetManagementPolicyAndRoute()
    {
        var type = typeof(DatasetDiagnosticsController);

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Policy.Should().Be(AuthorizationPolicies.CanManageDatasets);

        var routeAttr = type.GetCustomAttribute<RouteAttribute>();
        routeAttr.Should().NotBeNull();
        routeAttr!.Template.Should().Be("api/v1/datasets");

        var method = type.GetMethod(nameof(DatasetDiagnosticsController.DryRun));
        var rateLimitAttr = method!.GetCustomAttribute<EnableRateLimitingAttribute>();
        rateLimitAttr.Should().NotBeNull();
        rateLimitAttr!.PolicyName.Should().Be("test-conn");
    }

    [Fact]
    public async Task DryRun_WhenInvoked_ShouldReturn200OkWithResultDto()
    {
        // Arrange
        var sut = CreateSut();
        var columns = new List<DatasetColumnSchemaDto> { new("id", "integer", false) };
        var sampleRows = new List<IDictionary<string, object?>> { new Dictionary<string, object?> { ["id"] = 1 } };
        var expectedResult = new DryRunDatasetResultDto(true, columns, sampleRows, 1, 15);

        _dryRunUseCase.ExecuteAsync(Arg.Any<DryRunDatasetCommand>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = new DryRunDatasetRequest(
            DataConnectionId: Guid.NewGuid(),
            Provider: null,
            ConnectionString: null,
            SqlQuery: "SELECT 1 AS id",
            Parameters: null,
            MaxSampleRows: 5);

        // Act
        var result = await sut.DryRun(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var body = okResult.Value.Should().BeOfType<ApiResponse<DryRunDatasetResultDto>>().Subject;
        body.Data.Success.Should().BeTrue();
        body.Data.Columns.Should().HaveCount(1);
        body.Data.SampleRows.Should().HaveCount(1);
    }
}

