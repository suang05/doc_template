using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;

public sealed class UpdateTemplateDetailsUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly UpdateTemplateDetailsCommandValidator _validator = new();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public UpdateTemplateDetailsUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private UpdateTemplateDetailsUseCase CreateSut() => new(
        _templateRepo,
        _unitOfWork,
        _validator,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingDb()
    {
        // Arrange
        var invalidCommand = new UpdateTemplateDetailsCommand(Guid.Empty, "", null);

        // Act
        var act = () => CreateSut().ExecuteAsync(invalidCommand);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _templateRepo.DidNotReceive().GetByIdAsync(Arg.Any<TemplateId>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        _templateRepo.GetByIdAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns((Template?)null);

        var command = new UpdateTemplateDetailsCommand(templateId, "New Name", "New Category");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _templateRepo.DidNotReceive().Update(Arg.Any<Template>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_ShouldUpdateDetailsAndCommit()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        Template template = TemplateBuilder.ATemplate()
            .WithId(templateId)
            .WithName("Old Name")
            .WithSlug("slug");

        _templateRepo.GetByIdAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);

        var command = new UpdateTemplateDetailsCommand(templateId, "Updated Name", "Updated Category");

        // Act
        TemplateResultDto result = await CreateSut().ExecuteAsync(command);

        // Assert
        template.Name.Value.Should().Be("Updated Name");
        template.Category.Should().Be("Updated Category");
        result.Name.Should().Be("Updated Name");
        result.Category.Should().Be("Updated Category");

        _templateRepo.Received(1).Update(template);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
