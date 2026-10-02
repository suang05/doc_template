
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;

namespace SmkDoc.Application.Common.Interfaces;

public interface IGenerationLogMetricsRepository
{
    Task<LogMetricsDto> GetMetricsAsync(DateTimeOffset? startDate, DateTimeOffset? endDate,
        CancellationToken ct = default);
}
