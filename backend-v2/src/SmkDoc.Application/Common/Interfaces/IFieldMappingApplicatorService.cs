using System.Text.Json;
using SmkDoc.Application.Common.Models;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Common.Interfaces;

public interface IFieldMappingApplicatorService
{
    /// <param name="datasetAliases">
    /// Alias → resolved dataset (provider, connection string, SQL query).
    /// Built by the calling use case from the template's TemplateDataset assignments.
    /// Pass an empty dictionary when the template has no SQL mappings.
    /// </param>
    Task<string> ApplyAsync(
        JsonElement root,
        IEnumerable<FieldMapping> mappings,
        IReadOnlyDictionary<string, ResolvedDataset> datasetAliases);
}
