using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateHtmlSource;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries;

public sealed class GetTemplateHtmlSourceUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();

    private GetTemplateHtmlSourceUseCase CreateSut() => new(_templateRepo, _storageService);

    [Fact]
    public async Task ExecuteAsync_ShouldDownloadFromStorage_WhenHtmlTemplateExists()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        Template template = TemplateBuilder.ATemplate()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithSlug("receipt-template")
            .WithName("receipt-template");
        template.SetCurrentVersion(versionId, TestConstants.BaselineTime);

        TemplateVersion version = TemplateVersionBuilder.AVersion()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(1)
            .WithStorageKey("templates/receipt.html")
            .WithFormat(TemplateFormat.Html);

        _templateRepo.GetByIdAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);
        _templateRepo.GetVersionByIdAsync(versionId, Arg.Any<CancellationToken>())
            .Returns(version);

        string expectedHtml = "<html><body><h1>Receipt</h1></body></html>";
        byte[] htmlBytes = Encoding.UTF8.GetBytes(expectedHtml);
        _storageService.DownloadAsync(StorageBuckets.Templates, "templates/receipt.html", Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream(htmlBytes));

        var query = new GetTemplateHtmlSourceQuery(templateId);

        // Act
        var result = await CreateSut().ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().Be(expectedHtml);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowNotFoundException_WhenTemplateDoesNotExist()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        _templateRepo.GetByIdAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns((Template?)null);

        var query = new GetTemplateHtmlSourceQuery(templateId);

        // Act
        var act = () => CreateSut().ExecuteAsync(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowBusinessRuleViolationException_WhenTemplateHasNoActiveVersion()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        Template template = TemplateBuilder.ATemplate()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithSlug("receipt-template")
            .WithName("receipt-template")
            .WithoutCurrentVersion();

        _templateRepo.GetByIdAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);

        var query = new GetTemplateHtmlSourceQuery(templateId);

        // Act
        var act = () => CreateSut().ExecuteAsync(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleViolationException>()
            .WithMessage($"Template '{templateId}' has no active published version.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowBusinessRuleViolationException_WhenTemplateIsNotHtml()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        Template template = TemplateBuilder.ATemplate()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithSlug("docx-template")
            .WithName("docx-template");
        template.SetCurrentVersion(versionId, TestConstants.BaselineTime);

        TemplateVersion version = TemplateVersionBuilder.AVersion()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(1)
            .WithStorageKey("templates/template.docx")
            .WithFormat(TemplateFormat.Docx);

        _templateRepo.GetByIdAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);
        _templateRepo.GetVersionByIdAsync(versionId, Arg.Any<CancellationToken>())
            .Returns(version);

        var query = new GetTemplateHtmlSourceQuery(templateId);

        // Act
        var act = () => CreateSut().ExecuteAsync(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleViolationException>()
            .WithMessage("Only HTML templates can be opened in the Monaco editor.");
    }
}
