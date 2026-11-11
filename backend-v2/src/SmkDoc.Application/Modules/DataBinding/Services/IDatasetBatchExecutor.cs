using System.Text.Json;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;

namespace SmkDoc.Application.Modules.DataBinding.Services;

/// <summary>
/// Service interface for bounded concurrent execution of SQL datasets with caching and fail-fast cancellation.
/// </summary>
public interface IDatasetBatchExecutor
{
    Task<IReadOnlyDictionary<string, JsonElement>> ExecuteBatchAsync(
        IReadOnlyDictionary<string, ResolvedDatasetContext> datasetAliases,
        IReadOnlyDictionary<string, object?> callerContext,
        Guid? projectId,
        CancellationToken ct = default);
}
