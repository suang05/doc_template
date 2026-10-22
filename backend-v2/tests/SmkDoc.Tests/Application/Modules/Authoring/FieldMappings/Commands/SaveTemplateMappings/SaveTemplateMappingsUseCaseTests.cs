using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;

public sealed class SaveTemplateMappingsUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private SaveTemplateMappingsUseCase CreateSut() =>
        new(_templateRepo, _unitOfWork, _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_SavesMappingsAndCommits()
    {
        var templateId = Guid.NewGuid();
        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithName("Contract")
            .WithSlug("contract")
            .Build();

        _templateRepo.GetByIdWithDetailsAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(template);

        var items = new List<SaveFieldMappingItemDto>
        {
            new("customerName", "customer.name", "ชื่อลูกค้า", true, null, "thai_baht_text", 1)
        };

        var command = new SaveTemplateMappingsCommand(templateId, items);

        // Act
        await CreateSut().ExecuteAsync(command);

        // Assert
        template.FieldMappings.Should().HaveCount(1);
        var mapping = template.FieldMappings.First();
        mapping.TemplateId.Should().Be(templateId);
        mapping.Placeholder.Should().Be("customerName");
        mapping.SourcePath.Should().Be("customer.name");
        mapping.Transform.Should().Be("thai_baht_text");
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        _templateRepo.GetByIdWithDetailsAsync(templateId, Arg.Any<CancellationToken>())
            .Returns((Template?)null);
        _templateRepo.GetByIdAsync(templateId, Arg.Any<CancellationToken>())
            .Returns((Template?)null);

        var command = new SaveTemplateMappingsCommand(templateId, []);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
