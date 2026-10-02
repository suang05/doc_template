namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

public record ListGenerationLogsQuery(
    int Page = 1,
    int Limit = 50,
    string? CallerApp = null
);
