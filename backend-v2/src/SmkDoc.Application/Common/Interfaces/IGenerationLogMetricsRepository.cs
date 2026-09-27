using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmkDoc.Application.DTOs;
using SmkDoc.Application.DTOs.Logs;

namespace SmkDoc.Application.Common.Interfaces;

public interface IGenerationLogMetricsRepository
{
    Task<LogMetricsDto> GetMetricsAsync(DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken ct = default);
}
