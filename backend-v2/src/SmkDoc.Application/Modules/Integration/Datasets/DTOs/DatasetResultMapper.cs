using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.Integration.Datasets.DTOs;

/// <summary>
/// Maps <see cref="Dataset"/> entities to <see cref="DatasetResultDto"/>.
/// </summary>
public static class DatasetResultMapper
{
    public static DatasetResultDto ToResultDto(this Dataset dataset, string? connectionName = null)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        return new DatasetResultDto(
            dataset.Id.Value,
            dataset.Name.Value,
            dataset.Description,
            dataset.DataConnectionId.Value,
            connectionName ?? dataset.DataConnection?.Name.Value ?? string.Empty,
            dataset.SqlQuery,
            dataset.CacheSeconds,
            dataset.CreatedAt,
            dataset.UpdatedAt,
            dataset.Version);
    }
}
