using Moq;
using Microsoft.Extensions.Time.Testing;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ActivateTemplateVersion;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Fixtures;

public class TemplateAuthoringTestFixture
{
    public Mock<ITemplateRepository> TemplateRepo { get; } = new();
    public Mock<IStorageService> Storage { get; } = new();
    public Mock<IDocxSecurityScanner> Security { get; } = new();
    public Mock<IExecutionContext> Context { get; } = new();
    public Mock<IUnitOfWork> Uow { get; } = new();
    public Mock<ITemplateDraftCache> DraftCache { get; } = new();
    public Mock<IProjectRepository> ProjectRepo { get; } = new();
    public Mock<ISchemaInferenceService> SchemaInference { get; } = new();
    public Mock<ICurrentProjectContext> ProjectContext { get; } = new();
    public FakeTimeProvider Clock { get; } = TestConstants.CreateFakeClock();

    public static readonly Guid TestUserId = TestConstants.DefaultUserId;

    public TemplateAuthoringTestFixture()
    {
        Context.Setup(c => c.UserId).Returns(TestUserId);
        Uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var sampleProject = Project.Create(Guid.NewGuid(), ProjectName.Create("Default Project"),
            TemplateSlug.Create("default-proj"), Clock.GetUtcNow());
        ProjectRepo.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleProject);
    }

    public CreateTemplateUseCase BuildCreateTemplateUseCase() => new(
        TemplateRepo.Object,
        Storage.Object,
        Security.Object,
        Context.Object,
        Uow.Object,
        new CreateTemplateCommandValidator(),
        Clock
    );

    public CommitTemplateDraftUseCase BuildCommitTemplateDraftUseCase() => new(
        DraftCache.Object,
        Storage.Object,
        TemplateRepo.Object,
        Uow.Object,
        ProjectRepo.Object,
        SchemaInference.Object,
        Context.Object,
        ProjectContext.Object,
        Clock
    );

    public ActivateTemplateVersionUseCase BuildActivateTemplateVersionUseCase() => new(
        TemplateRepo.Object,
        Uow.Object,
        Clock
    );

    public UpdateTemplateDetailsUseCase BuildUpdateTemplateDetailsUseCase() => new(
        TemplateRepo.Object,
        Uow.Object,
        new UpdateTemplateDetailsCommandValidator(),
        Clock
    );

    public GetTemplateByIdUseCase BuildGetTemplateByIdUseCase() => new(
        TemplateRepo.Object
    );

    public RollbackTemplateVersionUseCase BuildRollbackTemplateVersionUseCase() => new(
        TemplateRepo.Object,
        Storage.Object,
        Context.Object,
        Uow.Object,
        Clock
    );

    public DeactivateTemplateUseCase BuildDeactivateTemplateUseCase() => new(
        TemplateRepo.Object,
        Uow.Object,
        Clock
    );
}
