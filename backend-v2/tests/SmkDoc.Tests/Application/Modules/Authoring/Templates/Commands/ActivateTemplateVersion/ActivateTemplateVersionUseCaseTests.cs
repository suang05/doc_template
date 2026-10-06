using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ActivateTemplateVersion;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.ActivateTemplateVersion;

public class ActivateTemplateVersionUseCaseTests
{
    private readonly TemplateAuthoringTestFixture _fixture = new();

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        _fixture.TemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        var useCase = _fixture.BuildActivateTemplateVersionUseCase();
        var act = () => useCase.ExecuteAsync(new ActivateTemplateVersionCommand(templateId, versionId));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenVersionBelongsToAnotherTemplate_ShouldThrowConflictException()
    {
        var templateId = Guid.NewGuid();
        var foreignTemplateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var template = new TemplateBuilder().WithId(templateId).Build();
        var foreignVersion = new TemplateVersionBuilder()
            .WithId(versionId)
            .WithTemplateId(foreignTemplateId)
            .Build();

        _fixture.TemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _fixture.VersionRepo
            .Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignVersion);

        var useCase = _fixture.BuildActivateTemplateVersionUseCase();
        var act = () => useCase.ExecuteAsync(new ActivateTemplateVersionCommand(templateId, versionId));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_ShouldEnforceDomainInvariantsAndCommit()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var template = new TemplateBuilder().WithId(templateId).Build();
        var version = new TemplateVersionBuilder()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(2)
            .Build();

        _fixture.TemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _fixture.VersionRepo
            .Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        var useCase = _fixture.BuildActivateTemplateVersionUseCase();
        var response = await useCase.ExecuteAsync(new ActivateTemplateVersionCommand(templateId, versionId));

        template.CurrentVersionId.Should().Be(versionId);
        template.IsActive.Should().BeTrue();
        response.CurrentVersionId.Should().Be(versionId);
        response.IsActive.Should().BeTrue();

        _fixture.TemplateRepo.Verify(r => r.Update(template), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
