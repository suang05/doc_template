using System.Text.Json;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public interface IDocumentDataPreparationService
{
    Task<PreparedDocumentData> PrepareDataAsync(
        TemplateVersion currentVersion,
        JsonElement rawData,
        bool skipValidation,
        CancellationToken ct = default);
}
