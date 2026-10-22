using SmkDoc.Application.Modules.Authoring.Templates.Commands.ActivateTemplateVersion;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.ActivateTemplateVersion;

public sealed class ActivateTemplateVersionUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public ActivateTemplateVersionUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private ActivateTemplateVersionUseCase CreateSut() => new(
        _templateRepo,
        _unitOfWork,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        _templateRepo.GetByIdAsync(templateId, Arg.Any<CancellationToken>())
            .Returns((Template?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new ActivateTemplateVersionCommand(templateId, versionId));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenVersionBelongsToAnotherTemplate_ShouldThrowConflictException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var foreignTemplateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        Template template = TemplateBuilder.ATemplate().WithId(templateId);
        TemplateVersion foreignVersion = TemplateVersionBuilder.AVersion()
            .WithId(versionId)
            .WithTemplateId(foreignTemplateId);

        _templateRepo.GetByIdAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(template);
        _templateRepo.GetVersionByIdAsync(versionId, Arg.Any<CancellationToken>())
            .Returns(foreignVersion);

        // Act
        var act = () => CreateSut().ExecuteAsync(new ActivateTemplateVersionCommand(templateId, versionId));

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _templateRepo.DidNotReceive().Update(Arg.Any<Template>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_ShouldEnforceDomainInvariantsAndCommit()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        Template template = TemplateBuilder.ATemplate().WithId(templateId);
        TemplateVersion version = TemplateVersionBuilder.AVersion()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(2);

        _templateRepo.GetByIdAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(template);
        _templateRepo.GetVersionByIdAsync(versionId, Arg.Any<CancellationToken>())
            .Returns(version);

        // Act
        TemplateResultDto result = await CreateSut().ExecuteAsync(new ActivateTemplateVersionCommand(templateId, versionId));

        // Assert
        template.CurrentVersionId.Should().Be(versionId);
        template.IsActive.Should().BeTrue();
        result.CurrentVersionId.Should().Be(versionId);
        result.IsActive.Should().BeTrue();

        _templateRepo.Received(1).Update(template);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
