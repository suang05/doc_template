using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Services;

/// <summary>
/// Domain service interface for dataset connection validation and batch alias resolution.
/// </summary>
public interface IDatasetResolver
{
    /// <summary>
    /// Validates read-only SQL invariants and resolves the target data connection.
    /// </summary>
    Task<DataConnection> ValidateAndResolveConnectionAsync(
        DataConnectionId connectionId,
        string sqlQuery,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves and decrypts all dataset connection bindings assigned to a template in batch.
    /// </summary>
    Task<IReadOnlyDictionary<string, ResolvedDatasetContext>> ResolveAssignmentsAsync(
        IEnumerable<TemplateDataset> assignments,
        CancellationToken ct = default);
}

