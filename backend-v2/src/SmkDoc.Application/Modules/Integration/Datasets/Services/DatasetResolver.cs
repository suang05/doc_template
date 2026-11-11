using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Services;

/// <summary>
/// Authoritative domain service for dataset connection validation, SSRF target inspection, and batch alias resolution.
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

    public async Task<EncryptedConnectionString> ValidateAndEncryptAsync(
        string provider,
        string connectionString,
        CancellationToken ct = default)
    {
        await sqlValidator.ValidateConnectionTargetAsync(provider, connectionString, ct);
        return EncryptedConnectionString.Create(dataProtection.Encrypt(connectionString));
    }

    public async Task<IReadOnlyDictionary<string, ResolvedDatasetContext>> ResolveAssignmentsAsync(
        IEnumerable<TemplateDataset> assignments,
        CancellationToken ct = default)
    {
        var assignmentList = assignments as IReadOnlyList<TemplateDataset> ?? assignments.ToList();
        if (assignmentList.Count == 0) return new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);

        var datasets = await LoadDatasetsAsync(assignmentList, ct);
        if (datasets.Count == 0) return new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);

        var fallbackConnections = await LoadFallbackConnectionsAsync(datasets, ct);
        return BuildResolvedContexts(assignmentList, datasets.ToDictionary(d => d.Id), fallbackConnections);
    }

    private async Task<IReadOnlyList<Dataset>> LoadDatasetsAsync(IReadOnlyList<TemplateDataset> assignments, CancellationToken ct)
    {
        var ids = assignments.Select(td => td.DatasetId).Distinct().ToList();
        return await datasetRepo.GetByIdsAsync(ids, ct) ?? [];
    }

    private async Task<Dictionary<DataConnectionId, DataConnection>> LoadFallbackConnectionsAsync(IEnumerable<Dataset> datasets, CancellationToken ct)
    {
        var missingIds = datasets.Where(d => d.DataConnection is null).Select(d => d.DataConnectionId).Distinct().ToList();
        if (missingIds.Count == 0) return [];
        var fetched = await connectionRepo.GetByIdsAsync(missingIds, ct) ?? [];
        return fetched.ToDictionary(c => c.Id);
    }

    private Dictionary<string, ResolvedDatasetContext> BuildResolvedContexts(
        IReadOnlyList<TemplateDataset> assignments,
        Dictionary<DatasetId, Dataset> datasetsById,
        Dictionary<DataConnectionId, DataConnection> fallbackConns)
    {
        var decryptedMap = new Dictionary<DataConnectionId, string>();
        var result = new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);

        foreach (var td in assignments)
        {
            if (!datasetsById.TryGetValue(td.DatasetId, out var dataset)) continue;
            var conn = dataset.DataConnection ?? fallbackConns.GetValueOrDefault(dataset.DataConnectionId);
            if (conn is null) continue;

            var decrypted = GetOrDecryptConnection(conn, decryptedMap);
            result[td.Alias.Value] = new ResolvedDatasetContext(
                conn.Provider.Name, decrypted, dataset.SqlQuery, dataset.Id.Value, conn.Id.Value, dataset.CacheSeconds);
        }
        return result;
    }

    private string GetOrDecryptConnection(DataConnection connection, Dictionary<DataConnectionId, string> cache)
    {
        if (cache.TryGetValue(connection.Id, out var existing)) return existing;
        try
        {
            var decrypted = dataProtection.Decrypt(connection.EncryptedConnectionString);
            cache[connection.Id] = decrypted;
            return decrypted;
        }
        catch (Exception ex) when (ex is not DataConnectionDecryptionException)
        {
            throw new DataConnectionDecryptionException(connection.Id.Value, connection.Name.Value, ex);
        }
    }
}

