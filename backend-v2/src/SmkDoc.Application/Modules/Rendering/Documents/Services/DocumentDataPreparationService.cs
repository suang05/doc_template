using System.Text.Json;
using SmkDoc.Application.Modules.DataBinding.Services;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

/// <summary>
/// Prepares and validates document data for rendering by dispatching to the unified <see cref="IDataBindingEngine"/>.
/// </summary>
public sealed class DocumentDataPreparationService(
    IDataBindingEngine dataBindingEngine) : IDocumentDataPreparationService
{
    public async Task<PreparedDocumentData> PrepareDataAsync(
        Template template,
        TemplateVersion currentVersion,
        JsonElement rawData,
        bool skipValidation,
        CancellationToken ct = default)
    {
        var bound = await dataBindingEngine.BindAndValidateAsync(
            template, currentVersion, rawData, skipValidation, ct);

        return new PreparedDocumentData(bound.DataJson, bound.ValidationResult);
    }
}
