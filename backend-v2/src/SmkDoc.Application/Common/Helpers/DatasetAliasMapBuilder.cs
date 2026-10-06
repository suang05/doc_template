using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Common.Helpers;

public static class DatasetAliasMapBuilder
{
    /// <summary>
    /// Builds alias → ResolvedDatasetContext map for a template.
    /// Called by use cases before invoking IFieldMappingApplicatorService.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, ResolvedDatasetContext>> BuildAsync(
        IEnumerable<TemplateDataset> assignments,
        IDatasetRepository datasetRepo,
        IDataConnectionRepository connectionRepo,
        IDataProtectionService dataProtection,
        CancellationToken ct)
    {
        var assignmentList = assignments as IReadOnlyList<TemplateDataset> ?? assignments.ToList();
        if (assignmentList.Count == 0)
            return new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);

        foreach (var td in assignmentList)
        {
            var dataset = await datasetRepo.GetByIdAsync(td.DatasetId, ct);
            if (dataset == null) continue;

            var connection = await connectionRepo.GetByIdAsync(dataset.DataConnectionId, ct);
            if (connection == null) continue;

            try
            {
                var cs = dataProtection.Decrypt(connection.EncryptedConnectionString);
                result[td.Alias.Value] = new ResolvedDatasetContext(connection.Provider.Name, cs, dataset.SqlQuery);
            }
            catch { /* skip if decryption fails */ }
        }

        return result;
    }
}
