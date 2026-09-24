using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Common.Helpers;

public static class DatasetAliasMapBuilder
{
    /// <summary>
    /// Builds alias → ResolvedDataset map for a template.
    /// Called by use cases before invoking IFieldMappingApplicatorService.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, ResolvedDataset>> BuildAsync(
        Guid templateId,
        IRepository<TemplateDataset> tdRepo,
        IRepository<Dataset> datasetRepo,
        IRepository<DataConnection> connectionRepo,
        IDataProtectionService dataProtection,
        CancellationToken ct)
    {
        var assignments = await tdRepo.ListAsync(td => td.TemplateId == templateId, ct);
        if (assignments.Count == 0)
            return new Dictionary<string, ResolvedDataset>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, ResolvedDataset>(StringComparer.OrdinalIgnoreCase);

        foreach (var td in assignments)
        {
            var dataset = await datasetRepo.GetByIdAsync(td.DatasetId, ct);
            if (dataset == null) continue;

            var connection = await connectionRepo.GetByIdAsync(dataset.DataConnectionId, ct);
            if (connection == null) continue;

            try
            {
                var cs = dataProtection.Decrypt(connection.EncryptedConnectionString);
                result[td.Alias] = new ResolvedDataset(connection.Provider, cs, dataset.SqlQuery);
            }
            catch { /* skip if decryption fails */ }
        }

        return result;
    }
}
