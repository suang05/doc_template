using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Tests.Common.Fixtures;

public class GenerateDocumentTestFixture
{
    public Mock<IRepository<Template>> TemplateRepo { get; } = new();
    public Mock<IRepository<TemplateVersion>> VersionRepo { get; } = new();
    public Mock<IRepository<FieldMapping>> MappingRepo { get; } = new();
    public Mock<IRepository<TemplateDataset>> TdRepo { get; } = new();
    public Mock<IRepository<Dataset>> DatasetRepo { get; } = new();
    public Mock<IRepository<DataConnection>> ConnectionRepo { get; } = new();
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
    }

    public GenerateDocumentUseCase BuildUseCase() => new(
        TemplateRepo.Object,
        VersionRepo.Object,
        MappingRepo.Object,
        TdRepo.Object,
        DatasetRepo.Object,
        ConnectionRepo.Object,
        LogRepo.Object,
        DocumentRepo.Object,
        DocVersionRepo.Object,
        Storage.Object,
        Engines,
        Context.Object,
        Uow.Object,
        Applicator.Object,
        DataProtection.Object,
        SchemaValidation.Object
    );
}
