using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Services;

/// <summary>
/// Authoritative domain service for dataset connection validation and batch alias resolution.
/// </summary>
public sealed class DatasetResolver(
    IDataConnectionRepository connectionRepo,
    IDatasetRepository datasetRepo,
    ISqlConnectionValidator sqlValidator,
    IDataProtectionService dataProtection) : IDatasetResolver
{
    public async Task<DataConnection> ValidateAndResolveConnectionAsync(
        DataConnectionId connectionId,
        string sqlQuery,
        CancellationToken ct = default)
    {
        sqlValidator.ValidateReadOnlySql(sqlQuery);

        return await connectionRepo.GetByIdAsync(connectionId, ct)
            ?? throw new NotFoundException($"DataConnection '{connectionId.Value}' not found.");
    }

    public async Task<IReadOnlyDictionary<string, ResolvedDatasetContext>> ResolveAssignmentsAsync(
        IEnumerable<TemplateDataset> assignments,
        CancellationToken ct = default)
    {
        var assignmentList = assignments as IReadOnlyList<TemplateDataset> ?? assignments.ToList();
        if (assignmentList.Count == 0)
        {
            return new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);
        }

        var distinctDatasetIds = assignmentList
            .Select(td => td.DatasetId)
            .Distinct()
            .ToList();

        var datasets = await datasetRepo.GetByIdsAsync(distinctDatasetIds, ct) ?? [];
        if (datasets.Count == 0)
        {
            return new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);
        }

        var datasetsById = datasets.ToDictionary(d => d.Id);

        var missingConnectionIds = datasets
            .Where(d => d.DataConnection is null)
            .Select(d => d.DataConnectionId)
            .Distinct()
            .ToList();

        Dictionary<DataConnectionId, DataConnection>? fallbackConnectionsById = null;
        if (missingConnectionIds.Count > 0)
        {
            var fetchedConnections = await connectionRepo.GetByIdsAsync(missingConnectionIds, ct) ?? [];
            fallbackConnectionsById = fetchedConnections.ToDictionary(c => c.Id);
        }

        var decryptedByConnectionId = new Dictionary<DataConnectionId, string>();
        var result = new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);

        foreach (var td in assignmentList)
        {
            if (!datasetsById.TryGetValue(td.DatasetId, out var dataset))
            {
                continue;
            }

            var connection = dataset.DataConnection
                ?? (fallbackConnectionsById != null && fallbackConnectionsById.TryGetValue(dataset.DataConnectionId, out var fallback)
                    ? fallback
                    : null);

            if (connection is null)
            {
                continue;
            }

            if (!decryptedByConnectionId.TryGetValue(connection.Id, out var decryptedConnectionString))
            {
                try
                {
                    decryptedConnectionString = dataProtection.Decrypt(connection.EncryptedConnectionString);
                    decryptedByConnectionId[connection.Id] = decryptedConnectionString;
                }
                catch (Exception ex) when (ex is not DataConnectionDecryptionException)
                {
                    throw new DataConnectionDecryptionException(connection.Id.Value, connection.Name.Value, ex);
                }
            }

            result[td.Alias.Value] = new ResolvedDatasetContext(
                Provider: connection.Provider.Name,
                ConnectionString: decryptedConnectionString,
                SqlQuery: dataset.SqlQuery,
                DatasetId: dataset.Id.Value,
                DataConnectionId: connection.Id.Value,
                CacheSeconds: dataset.CacheSeconds);
        }

        return result;
    }
}

