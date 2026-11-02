using System.Text;
using System.Text.Json;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;
using SmkDoc.Application.Modules.Authoring.Schemas.Commands.ValidateStandaloneSchema;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.RenderStatelessDocument;

namespace SmkDoc.Tests.Application.Common.Behaviors;

public sealed class PhaseP1CommandValidatorsTests
{
    [Fact]
    public async Task RenderStatelessDocumentCommandValidator_ValidatesStreamAndFileName()
    {
        var validator = new RenderStatelessDocumentCommandValidator();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<h1>Hello</h1>"));

        (await validator.ValidateAsync(new RenderStatelessDocumentCommand(stream, "invoice.html"))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new RenderStatelessDocumentCommand(null!, ""))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDataConnectionCommandValidator_ValidatesNonEmptyId()
    {
        var validator = new DeleteDataConnectionCommandValidator();

        (await validator.ValidateAsync(new DeleteDataConnectionCommand(Guid.NewGuid()))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new DeleteDataConnectionCommand(Guid.Empty))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task TestDataConnectionCommandValidator_ValidatesProviderAndConnectionString()
    {
        var validator = new TestDataConnectionCommandValidator();

        (await validator.ValidateAsync(new TestDataConnectionCommand("PostgreSQL", "Host=db;Database=app"))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new TestDataConnectionCommand("SQLite", ""))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDatasetCommandValidator_ValidatesNonEmptyId()
    {
        var validator = new DeleteDatasetCommandValidator();

        (await validator.ValidateAsync(new DeleteDatasetCommand(Guid.NewGuid()))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new DeleteDatasetCommand(Guid.Empty))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeApiKeyCommandValidator_ValidatesNonEmptyId()
    {
        var validator = new RevokeApiKeyCommandValidator();

        (await validator.ValidateAsync(new RevokeApiKeyCommand(Guid.NewGuid()))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new RevokeApiKeyCommand(Guid.Empty))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task SaveTemplateDatasetsCommandValidator_ValidatesTemplateIdAndChildItems()
    {
        var validator = new SaveTemplateDatasetsCommandValidator();
        var validItem = new SaveTemplateDatasetItemDto(Guid.NewGuid(), "orders", 0);
        var invalidItem = new SaveTemplateDatasetItemDto(Guid.Empty, "", 0);

        (await validator.ValidateAsync(new SaveTemplateDatasetsCommand(Guid.NewGuid(), [validItem]))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new SaveTemplateDatasetsCommand(Guid.Empty, [invalidItem]))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task SaveTemplateMappingsCommandValidator_ValidatesTemplateIdAndPlaceholders()
    {
        var validator = new SaveTemplateMappingsCommandValidator();
        var validMapping = new SaveFieldMappingItemDto("customer_name", "customer.name", "Customer", true, null, null, 0);
        var invalidMapping = new SaveFieldMappingItemDto("", "customer.name", "Customer", true, null, null, 0);

        (await validator.ValidateAsync(new SaveTemplateMappingsCommand(Guid.NewGuid(), [validMapping]))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new SaveTemplateMappingsCommand(Guid.Empty, [invalidMapping]))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UploadFontCommandValidator_ValidatesStreamFileNameAndContentType()
    {
        var validator = new UploadFontCommandValidator();
        using var stream = new MemoryStream([1, 2, 3]);

        (await validator.ValidateAsync(new UploadFontCommand(stream, "Prompt.woff2", "font/woff2"))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new UploadFontCommand(null!, "", ""))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateStandaloneSchemaCommandValidator_ValidatesJsonSchemaAndPayloadKinds()
    {
        var validator = new ValidateStandaloneSchemaCommandValidator();
        using var schemaDoc = JsonDocument.Parse("""{"type":"object"}""");
        using var payloadDoc = JsonDocument.Parse("""{"id":1}""");
        using var arrayDoc = JsonDocument.Parse("""[1,2,3]""");
        using var nullDoc = JsonDocument.Parse("null");

        (await validator.ValidateAsync(new ValidateStandaloneSchemaCommand(schemaDoc.RootElement, payloadDoc.RootElement))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new ValidateStandaloneSchemaCommand(arrayDoc.RootElement, nullDoc.RootElement))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CommitTemplateDraftCommandValidator_ValidatesFlattenedDraftCommand()
    {
        var validator = new CommitTemplateDraftCommandValidator();

        (await validator.ValidateAsync(new CommitTemplateDraftCommand("draft-1", "Invoice", "tax-invoice", "Finance", []))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new CommitTemplateDraftCommand("", "A", "INVALID_SLUG", null, null!))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateTemplateCommandValidator_ValidatesTemplateId()
    {
        var validator = new DeactivateTemplateCommandValidator();

        (await validator.ValidateAsync(new DeactivateTemplateCommand(Guid.NewGuid()))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new DeactivateTemplateCommand(Guid.Empty))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ParseTemplateDraftCommandValidator_ValidatesStreamAndFileName()
    {
        var validator = new ParseTemplateDraftCommandValidator();
        using var stream = new MemoryStream([0x50, 0x4B]);

        (await validator.ValidateAsync(new ParseTemplateDraftCommand(stream, "template.docx"))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new ParseTemplateDraftCommand(null!, ""))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RollbackTemplateVersionCommandValidator_ValidatesTemplateIdAndPositiveTargetVersion()
    {
        var validator = new RollbackTemplateVersionCommandValidator();

        (await validator.ValidateAsync(new RollbackTemplateVersionCommand(Guid.NewGuid(), 2))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new RollbackTemplateVersionCommand(Guid.Empty, 0))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task SaveTemplateHtmlCommandValidator_ValidatesTemplateIdAndHtmlContent()
    {
        var validator = new SaveTemplateHtmlCommandValidator();

        (await validator.ValidateAsync(new SaveTemplateHtmlCommand(Guid.NewGuid(), "<html><body>Hi</body></html>"))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new SaveTemplateHtmlCommand(Guid.Empty, "   "))).IsValid.Should().BeFalse();
    }
}
