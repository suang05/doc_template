using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Controllers.Rendering;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogPayload;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class AuditLogControllerTests
{
    private readonly IUseCase<ListGenerationLogsQuery, GenerationLogPagedResult> _listUseCase =
        Substitute.For<IUseCase<ListGenerationLogsQuery, GenerationLogPagedResult>>();
    private readonly IUseCase<GetLogMetricsQuery, LogMetricsDto> _metricsUseCase =
        Substitute.For<IUseCase<GetLogMetricsQuery, LogMetricsDto>>();
    private readonly IUseCase<GetLogPayloadQuery, LogPayloadResultDto> _payloadUseCase =
        Substitute.For<IUseCase<GetLogPayloadQuery, LogPayloadResultDto>>();

    private AuditLogController CreateSut()
    {
        return new AuditLogController(
            _listUseCase,
            _metricsUseCase,
            _payloadUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void AuditLogController_ShouldBeDecoratedWithSecurityAndRoutingAttributes()
    {
        var type = typeof(AuditLogController);

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Policy.Should().Be(AuthorizationPolicies.RequireProjectAdmin);

        var routes = type.GetCustomAttributes<RouteAttribute>().Select(r => r.Template).ToList();
        routes.Should().ContainSingle().Which.Should().Be("api/v1/logs");
    }

    [Fact]
    public async Task ListLogs_ShouldReturnPagedApiResponse()
    {
        // Arrange
        var sut = CreateSut();
        var logs = new List<GenerationLogDto>
        {
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "billing-service",
                "direct",
                "pdf",
                20480,
                2,
                150,
                "Success",
                null,
                DateTimeOffset.UtcNow)
        };
        var pagedResult = new GenerationLogPagedResult(logs, 1, 1, 50);

        _listUseCase.ExecuteAsync(
                Arg.Is<ListGenerationLogsQuery>(q => q.Page == 1 && q.Limit == 50 && q.CallerApp == "billing-service"),
                Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await sut.ListLogs(page: 1, limit: 50, app: "billing-service", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResponse = okResult.Value.Should().BeOfType<PagedApiResponse<GenerationLogDto>>().Subject;
        pagedResponse.Data.Should().BeEquivalentTo(logs);
        pagedResponse.Pagination.TotalCount.Should().Be(1);
        pagedResponse.Pagination.Page.Should().Be(1);
        pagedResponse.Pagination.Limit.Should().Be(50);
    }

    [Fact]
    public async Task GetMetrics_ShouldReturnMetricsEnvelope()
    {
        // Arrange
        var sut = CreateSut();
        var startDate = DateTimeOffset.UtcNow.AddDays(-7);
        var endDate = DateTimeOffset.UtcNow;
        var metricsDto = new LogMetricsDto(
            TotalGenerations: 100,
            SuccessfulGenerations: 95,
            FailedGenerations: 5,
            AverageDurationMs: 124.5,
            TotalFileSizeBytes: 10485760,
            TopCallerApps: new List<AppUsageMetricDto>
            {
                new("billing-service", 60),
                new("crm-app", 40)
            });

        _metricsUseCase.ExecuteAsync(
                Arg.Is<GetLogMetricsQuery>(q => q.StartDate == startDate && q.EndDate == endDate),
                Arg.Any<CancellationToken>())
            .Returns(metricsDto);

        // Act
        var result = await sut.GetMetrics(startDate, endDate, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<LogMetricsDto>>().Subject;
        apiResponse.Data.Should().BeEquivalentTo(metricsDto);
    }

    [Fact]
    public async Task GetPayload_ShouldReturnPayloadEnvelope()
    {
        // Arrange
        var sut = CreateSut();
        var logId = Guid.NewGuid();
        var payloadDto = new LogPayloadResultDto(
            LogId: logId,
            PayloadJson: "{\"customerName\":\"Acme Corp\"}",
            IsOffloaded: false,
            OffloadedKey: null);

        _payloadUseCase.ExecuteAsync(
                Arg.Is<GetLogPayloadQuery>(q => q.LogId == logId),
                Arg.Any<CancellationToken>())
            .Returns(payloadDto);

        // Act
        var result = await sut.GetPayload(logId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<LogPayloadResultDto>>().Subject;
        apiResponse.Data.Should().BeEquivalentTo(payloadDto);
    }
}

