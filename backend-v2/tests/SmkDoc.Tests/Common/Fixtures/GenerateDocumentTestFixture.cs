using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

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
            Context.Object,
            Uow.Object);

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

    public void GivenTemplateWithVersion(Template template, TemplateVersion version, string htmlContent = "<html></html>")
    {
        TemplateRepo.Setup(r => r.GetBySlugWithDetailsAsync(template.Slug.Value, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        TemplateRepo.Setup(r => r.GetBySlugAsync(template.Slug.Value, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
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
