using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateStudioBundle;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries;

public sealed class GetTemplateStudioBundleUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();

    private GetTemplateStudioBundleUseCase CreateSut() => new(_templateRepo, _storageService);

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFullStudioBundle_WhenTemplateExists()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        Template template = TemplateBuilder.ATemplate()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithSlug("invoice-template")
            .WithName("invoice-template");
        template.SetCurrentVersion(versionId, TestConstants.BaselineTime);

        TemplateVersion version = TemplateVersionBuilder.AVersion()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(2)
            .WithStorageKey("templates/invoice.html")
            .WithFormat(TemplateFormat.Html);
        version.UpdateDataSchema("{\"type\": \"object\"}", "{\"total\": 1000}", TestConstants.BaselineTime);

        _templateRepo.GetByIdAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);
        _templateRepo.GetVersionByIdAsync(versionId, Arg.Any<CancellationToken>())
            .Returns(version);

        string expectedHtml = "<html><body>Invoice</body></html>";
        byte[] htmlBytes = Encoding.UTF8.GetBytes(expectedHtml);
        _storageService.DownloadAsync(StorageBuckets.Templates, "templates/invoice.html", Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream(htmlBytes));

        var query = new GetTemplateStudioBundleQuery(templateId);

        // Act
        var result = await CreateSut().ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Html.Should().Be(expectedHtml);
        result.SamplePayload.Should().Be("{\"total\": 1000}");
        result.DataSchema.Should().Be("{\"type\": \"object\"}");
        result.Version.Should().Be(2);
    }
}
