using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ActivateTemplateVersion;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Tests.Common.Fixtures;

public class TemplateAuthoringTestFixture
{
    public Mock<ITemplateRepository> TemplateRepo { get; } = new();
    public Mock<IRepository<TemplateVersion>> VersionRepo { get; } = new();
    public Mock<IStorageService> Storage { get; } = new();
    public Mock<IDocxSecurityScanner> Security { get; } = new();
    public Mock<IExecutionContext> Context { get; } = new();
    public Mock<IUnitOfWork> Uow { get; } = new();
    public Microsoft.Extensions.Time.Testing.FakeTimeProvider Clock { get; } = TestConstants.CreateFakeClock();

    public static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public TemplateAuthoringTestFixture()
    {
        Context.Setup(c => c.UserId).Returns(TestUserId);
    }

    public CreateTemplateUseCase BuildCreateTemplateUseCase() => new(
        TemplateRepo.Object,
        VersionRepo.Object,
        Storage.Object,
        Security.Object,
        Context.Object,
        Uow.Object,
        new CreateTemplateCommandValidator(),
        Clock
    );

    public ActivateTemplateVersionUseCase BuildActivateTemplateVersionUseCase() => new(
        TemplateRepo.Object,
        VersionRepo.Object,
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
        VersionRepo.Object,
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
