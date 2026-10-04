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
            Uow.Object
        );
}
