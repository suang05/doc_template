namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;

public record GetLogMetricsQuery(
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null
);
