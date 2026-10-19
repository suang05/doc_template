using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Common.Fixtures;

public class GenerateDocumentTestFixture
{
    public Mock<ITemplateRepository> TemplateRepo { get; } = new();
    public Mock<IRepository<TemplateVersion>> VersionRepo { get; } = new();
    public Mock<IDatasetRepository> DatasetRepo { get; } = new();
    public Mock<IDataConnectionRepository> ConnectionRepo { get; } = new();
    public Mock<IRepository<GenerationLog>> LogRepo { get; } = new();
    public Mock<IRepository<Document>> DocumentRepo { get; } = new();
    public Mock<IRepository<DocumentVersion>> DocVersionRepo { get; } = new();
    public Mock<IStorageService> Storage { get; } = new();
    public Mock<IRenderEngine> Engine { get; } = new();
    public Mock<IExecutionContext> Context { get; } = new();
    public Mock<IUnitOfWork> Uow { get; } = new();
    public Mock<IFieldMappingApplicatorService> Applicator { get; } = new();
    public Mock<IDataProtectionService> DataProtection { get; } = new();
    public Mock<IJsonSchemaValidationService> SchemaValidation { get; } = new();

    public List<IRenderEngine> Engines { get; } = new();

    public GenerateDocumentTestFixture()
    {
        Engines.Add(Engine.Object);
        Uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        Context.Setup(c => c.ProjectId).Returns(Guid.NewGuid());
    }

    public IDocumentDataPreparationService BuildDataPreparationService() =>
        new DocumentDataPreparationService(
            DatasetRepo.Object,
            ConnectionRepo.Object,
            DataProtection.Object,
            Applicator.Object,
            SchemaValidation.Object);

    public IDocumentAuditService BuildAuditService() =>
        new DocumentAuditService(
            LogRepo.Object,
            Context.Object,
            Uow.Object);

    public IDocumentVersioningService BuildVersioningService() =>
        new DocumentVersioningService(
            DocumentRepo.Object,
            DocVersionRepo.Object,
            Context.Object);

    public GenerateDocumentUseCase BuildUseCase(
        IDocumentDataPreparationService? dataPrep = null,
        IDocumentAuditService? audit = null,
        IDocumentVersioningService? versioning = null) => new(
            TemplateRepo.Object,
            VersionRepo.Object,
            Storage.Object,
            Engines,
            dataPrep ?? BuildDataPreparationService(),
            audit ?? BuildAuditService(),
            versioning ?? BuildVersioningService(),
            Uow.Object,
            Context.Object
        );

    public void GivenTemplate(Template template)
    {
        TemplateRepo.Setup(r => r.GetBySlugWithDetailsAsync(template.Slug.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
    }

    public void GivenInactiveTemplate(string slug)
    {
        var template = new TemplateBuilder().AsInactive().WithSlug(slug).Build();
        GivenTemplate(template);
    }

    public void GivenTemplateWithoutVersion(string slug)
    {
        var template = new TemplateBuilder().WithoutCurrentVersion().WithSlug(slug).Build();
        GivenTemplate(template);
    }

    public (Template Template, TemplateVersion Version) GivenActiveHtmlTemplate(
        string slug = "sale-contract",
        string name = "Sale Contract",
        string htmlContent = "<html><body>{{name}}</body></html>",
        int versionNumber = 1,
        Guid? templateId = null,
        Guid? versionId = null,
        DateTimeOffset? now = null)
    {
        var tId = templateId ?? Guid.NewGuid();
        var vId = versionId ?? Guid.NewGuid();
        var timestamp = now ?? TestConstants.BaselineTime;

        var template = TemplateBuilder.ATemplate()
            .WithId(tId)
            .WithName(name)
            .WithSlug(slug)
            .WithCurrentVersion(vId)
            .WithTime(timestamp)
            .Build();

        var version = TemplateVersionBuilder.AVersion()
            .WithId(vId)
            .WithTemplateId(tId)
            .WithVersion(versionNumber)
            .WithStorageKey($"templates/{slug}.html")
            .WithFormat(TemplateFormat.Html)
            .WithTime(timestamp)
            .Build();

        GivenTemplateWithVersion(template, version, htmlContent);
        return (template, version);
    }

    public void GivenTemplateWithVersion(Template template, TemplateVersion version, string htmlContent = "<html></html>")
    {
        GivenTemplate(template);
        VersionRepo.Setup(r => r.GetByIdAsync(version.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);
        Storage.Setup(s => s.DownloadAsync("templates", version.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(htmlContent)));
    }

    public void GivenRenderEnginePdfOutput(string output = "%PDF-1.4 Mock Output")
    {
        Engine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        Engine.Setup(e => e.RenderStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(output)));
    }

    public void GivenPresignedUrl(string url = "https://minio.sammakorn.co.th/outputs/sample.pdf")
    {
        Storage.Setup(s => s.GetPresignedUrlAsync("outputs", It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(url);
    }
}
