using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.FieldMappings;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Common.Helpers;

public static class DatasetAliasMapBuilder
{
    /// <summary>
    /// Builds alias → ResolvedDatasetContext map for a template.
    /// Called by use cases before invoking IFieldMappingApplicatorService.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, ResolvedDatasetContext>> BuildAsync(
        Guid templateId,
        IRepository<TemplateDataset> tdRepo,
        IRepository<Dataset> datasetRepo,
        IRepository<DataConnection> connectionRepo,
        IDataProtectionService dataProtection,
        CancellationToken ct)
    {
        var assignments = await tdRepo.ListAsync(td => td.TemplateId == templateId, ct);
        if (assignments.Count == 0)
            return new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);

        foreach (var td in assignments)
        {
            var dataset = await datasetRepo.GetByIdAsync(td.DatasetId, ct);
            if (dataset == null) continue;

            var connection = await connectionRepo.GetByIdAsync(dataset.DataConnectionId, ct);
            if (connection == null) continue;

            try
            {
                var cs = dataProtection.Decrypt(connection.EncryptedConnectionString);
                result[td.Alias] = new ResolvedDatasetContext(connection.Provider, cs, dataset.SqlQuery);
            }
            catch { /* skip if decryption fails */ }
        }

        return result;
    }
}
