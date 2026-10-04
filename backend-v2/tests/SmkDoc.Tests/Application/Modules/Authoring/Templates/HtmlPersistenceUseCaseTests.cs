using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using Xunit;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates;

public class HtmlPersistenceUseCaseTests
{
    private readonly Mock<ITemplateRepository>          _mockTemplateRepo    = new();
    private readonly Mock<IRepository<TemplateVersion>> _mockVersionRepo     = new();
    private readonly Mock<IStorageService>              _mockStorage         = new();
    private readonly Mock<IUnitOfWork>                  _mockUow             = new();
    private readonly Mock<IExecutionContext>             _mockContext         = new();
    private readonly Mock<ISchemaInferenceService>      _mockSchemaInference = new();

    [Fact]
    public async Task SaveHtmlVersionAsync_ShouldUploadArchivedAndActiveKeys_AndIncrementVersion_AndPersistSchema()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var currentVersionId = Guid.NewGuid();

        var template = new Template(Guid.NewGuid(), "official-contract", "official-contract", null, id: templateId);
        template.SetCurrentVersion(currentVersionId);

        var currentVersion = new TemplateVersion(templateId, 3, "", TemplateFormat.Html, null, id: currentVersionId);

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(currentVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentVersion);
        _mockContext.SetupGet(c => c.CallerApp).Returns("studio-user");

        string dummySchema = "{\"$schema\":\"http://json-schema.org/draft-07/schema#\",\"type\":\"object\"}";
        string dummySample = "{\"customerName\":\"คุณสมชาย\"}";

        _mockSchemaInference.Setup(s => s.InferFromHtml(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((dummySchema, dummySample));

        var useCase = new HtmlPersistenceUseCase(
            _mockTemplateRepo.Object,
            _mockVersionRepo.Object,
            _mockStorage.Object,
            _mockUow.Object,
            _mockContext.Object,
            _mockSchemaInference.Object
        );

        var request = new SaveTemplateHtmlCommand(
            Html: "<html><body><h1>Contract Version 4</h1></body></html>",
            SamplePayload: dummySample,
            ChangeNote: "Updated payment terms"
        );

        // Act
        int nextVersion = await useCase.SaveHtmlVersionAsync(templateId, request);

        // Assert
        nextVersion.Should().Be(4);

        // Verify versioned archive upload
        _mockStorage.Verify(s => s.UploadAsync(
            "templates",
            "templates/archive/official-contract_v4.html",
            It.IsAny<Stream>(),
            "text/html; charset=utf-8",
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify active upload
        _mockStorage.Verify(s => s.UploadAsync(
            "templates",
            "templates/official-contract.html",
            It.IsAny<Stream>(),
            "text/html; charset=utf-8",
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify database insert of new version with DataSchema and SamplePayload
        _mockVersionRepo.Verify(r => r.AddAsync(
            It.Is<TemplateVersion>(v => v.Version == 4 &&
                                        v.CommitMessage == "Updated payment terms" &&
                                        v.DataSchema == dummySchema &&
                                        v.SamplePayload == dummySample),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify UoW saved
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
