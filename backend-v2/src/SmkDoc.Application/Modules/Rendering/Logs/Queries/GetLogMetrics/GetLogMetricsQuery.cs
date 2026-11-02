namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;

public sealed record GetLogMetricsQuery(
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null
);
