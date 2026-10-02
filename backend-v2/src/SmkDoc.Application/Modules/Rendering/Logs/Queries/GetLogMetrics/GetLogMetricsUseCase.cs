using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;

namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;

public sealed class GetLogMetricsUseCase(IGenerationLogMetricsRepository metricsRepo)
    : IUseCase<GetLogMetricsQuery, LogMetricsDto>
{
    private readonly IGenerationLogMetricsRepository _metricsRepo = metricsRepo;

    public async Task<LogMetricsDto> ExecuteAsync(GetLogMetricsQuery query, CancellationToken ct = default)
    {
        return await _metricsRepo.GetMetricsAsync(query.StartDate, query.EndDate, ct);
    }
}
