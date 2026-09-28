using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ActivateTemplateVersion;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Templates;

public class ActivateTemplateVersionUseCaseTests
{
    private readonly Mock<ITemplateRepository> _mockTemplateRepo = new();
    private readonly Mock<IRepository<TemplateVersion>> _mockVersionRepo = new();
    private readonly Mock<IUnitOfWork> _mockUow = new();

    private ActivateTemplateVersionUseCase CreateUseCase() => new(
        _mockTemplateRepo.Object,
        _mockVersionRepo.Object,
        _mockUow.Object);

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        _mockTemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        var useCase = CreateUseCase();
        var act = () => useCase.ExecuteAsync(new ActivateTemplateVersionCommand(templateId, versionId));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenVersionBelongsToAnotherTemplate_ShouldThrowConflictException()
    {
        var templateId = Guid.NewGuid();
        var foreignTemplateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var template = new Template(Guid.NewGuid(), "Contract", "contract", null) { Id = templateId };
        var foreignVersion = new TemplateVersion(foreignTemplateId, 1, "key", TemplateFormat.Html, "author", "note") { Id = versionId };

        _mockTemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo
            .Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignVersion);

        var useCase = CreateUseCase();
        var act = () => useCase.ExecuteAsync(new ActivateTemplateVersionCommand(templateId, versionId));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_ShouldEnforceDomainInvariantsAndCommit()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var template = new Template(Guid.NewGuid(), "Contract", "contract", null) { Id = templateId };
        var version = new TemplateVersion(templateId, 2, "key_v2", TemplateFormat.Html, "author", "note") { Id = versionId };

        _mockTemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo
            .Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        var useCase = CreateUseCase();
        var response = await useCase.ExecuteAsync(new ActivateTemplateVersionCommand(templateId, versionId));

        template.CurrentVersionId.Should().Be(versionId);
        template.IsActive.Should().BeTrue();
        response.CurrentVersionId.Should().Be(versionId);
        response.IsActive.Should().BeTrue();

        _mockTemplateRepo.Verify(r => r.Update(template), Times.Once);
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
