using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.FieldMappings;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Api.Controllers.Authoring;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateHtmlSource;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateSchema;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateStudioBundle;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class TemplateStudioControllerTests
{
    private readonly IUseCase<GetTemplateHtmlSourceQuery, string> _getHtmlSourceUseCase = Substitute.For<IUseCase<GetTemplateHtmlSourceQuery, string>>();
    private readonly IUseCase<GetTemplateStudioBundleQuery, TemplateStudioDto> _getStudioBundleUseCase = Substitute.For<IUseCase<GetTemplateStudioBundleQuery, TemplateStudioDto>>();
    private readonly IUseCase<GetTemplateSchemaQuery, TemplateSchemaDto> _getSchemaUseCase = Substitute.For<IUseCase<GetTemplateSchemaQuery, TemplateSchemaDto>>();
    private readonly IUseCase<SaveTemplateHtmlCommand, SaveHtmlResultDto> _saveHtmlUseCase = Substitute.For<IUseCase<SaveTemplateHtmlCommand, SaveHtmlResultDto>>();
    private readonly IUseCase<ValidateTemplateHtmlQuery, TemplateValidationResultDto> _validateUseCase = Substitute.For<IUseCase<ValidateTemplateHtmlQuery, TemplateValidationResultDto>>();
    private readonly IUseCase<GetTemplateMappingsQuery, List<FieldMappingDto>> _getMappingsUseCase = Substitute.For<IUseCase<GetTemplateMappingsQuery, List<FieldMappingDto>>>();
    private readonly IUseCase<SaveTemplateMappingsCommand, List<FieldMappingDto>> _saveMappingsUseCase = Substitute.For<IUseCase<SaveTemplateMappingsCommand, List<FieldMappingDto>>>();
    private readonly IUseCase<PreviewMappingQuery, Stream> _previewMappingUseCase = Substitute.For<IUseCase<PreviewMappingQuery, Stream>>();

    private TemplateStudioController CreateSut()
    {
        var controller = new TemplateStudioController(
            _getHtmlSourceUseCase,
            _getStudioBundleUseCase,
            _getSchemaUseCase,
            _saveHtmlUseCase,
            _validateUseCase,
            _getMappingsUseCase,
            _saveMappingsUseCase,
            _previewMappingUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    [Fact]
    public async Task GetHtml_ShouldReturnHtmlContentResult()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        _getHtmlSourceUseCase.ExecuteAsync(Arg.Is<GetTemplateHtmlSourceQuery>(q => q.TemplateId == templateId), Arg.Any<CancellationToken>())
            .Returns("<html><body>Hello</body></html>");

        // Act
        var result = await sut.GetHtml(templateId, CancellationToken.None);

        // Assert
        var contentResult = result.Should().BeOfType<ContentResult>().Subject;
        contentResult.ContentType.Should().Be("text/html; charset=utf-8");
        contentResult.Content.Should().Be("<html><body>Hello</body></html>");
    }

    [Fact]
    public async Task GetStudioBundle_ShouldReturnStudioBundleEnvelope()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        var bundleDto = new TemplateStudioDto(
            "<html></html>",
            "{}",
            "{}",
            1);

        _getStudioBundleUseCase.ExecuteAsync(Arg.Is<GetTemplateStudioBundleQuery>(q => q.TemplateId == templateId), Arg.Any<CancellationToken>())
            .Returns(bundleDto);

        // Act
        var result = await sut.GetStudioBundle(templateId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<TemplateStudioDto>>().Subject;
        body.Data.Version.Should().Be(1);
    }

    [Fact]
    public async Task SaveHtml_WithIfMatchHeader_ShouldPassIfMatchAndSetETagHeader()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        _saveHtmlUseCase.ExecuteAsync(Arg.Is<SaveTemplateHtmlCommand>(c => c.TemplateId == templateId && c.IfMatch == "\"1\""), Arg.Any<CancellationToken>())
            .Returns(new SaveHtmlResultDto(2));

        var request = new SaveHtmlRequest("<html>updated</html>", "{}", "Increment note");

        // Act
        var result = await sut.SaveHtml(templateId, request, "\"1\"", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<SaveHtmlResultDto>>().Subject;
        body.Data.Version.Should().Be(2);
        sut.Response.Headers.ETag.ToString().Should().Be("\"2\"");
    }

    [Fact]
    public async Task Validate_ShouldReturnValidationResultEnvelope()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        var validationDto = new TemplateValidationResultDto(true, ["name", "amount"], []);
        _validateUseCase.ExecuteAsync(Arg.Any<ValidateTemplateHtmlQuery>(), Arg.Any<CancellationToken>())
            .Returns(validationDto);

        var request = new ValidateHtmlRequest("<html>{{name}}</html>");

        // Act
        var result = await sut.Validate(templateId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<TemplateValidationResultDto>>().Subject;
        body.Data.Valid.Should().BeTrue();
        body.Data.Fields.Should().Contain("name");
    }

    [Fact]
    public async Task GetMappings_ShouldReturnMappingsCollection()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        var mappings = new List<FieldMappingDto>
        {
            new(Guid.NewGuid(), templateId, "company", "req.company", "Company", false, null, null, 1, "direct", null, null, null)
        };
        _getMappingsUseCase.ExecuteAsync(Arg.Is<GetTemplateMappingsQuery>(q => q.TemplateId == templateId), Arg.Any<CancellationToken>())
            .Returns(mappings);

        // Act
        var result = await sut.GetMappings(templateId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<FieldMappingDto>>>().Subject;
        body.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task SaveMappings_ShouldExecuteCommandAndReturnUpdatedMappings()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        var mappings = new List<FieldMappingDto>
        {
            new(Guid.NewGuid(), templateId, "company", "req.company", "Company", false, null, null, 1, "direct", null, null, null)
        };
        _saveMappingsUseCase.ExecuteAsync(Arg.Is<SaveTemplateMappingsCommand>(c => c.TemplateId == templateId), Arg.Any<CancellationToken>())
            .Returns(mappings);

        var request = new SaveTemplateMappingsRequest([]);

        // Act
        var result = await sut.SaveMappings(templateId, request, null, CancellationToken.None);

        // Assert
        await _saveMappingsUseCase.Received(1).ExecuteAsync(Arg.Is<SaveTemplateMappingsCommand>(c => c.TemplateId == templateId), Arg.Any<CancellationToken>());
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<FieldMappingDto>>>().Subject;
        body.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task PreviewMappings_ShouldReturnInlinePdfStream()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        using var stream = new MemoryStream([1, 2, 3]);
        _previewMappingUseCase.ExecuteAsync(Arg.Is<PreviewMappingQuery>(q => q.TemplateId == templateId), Arg.Any<CancellationToken>())
            .Returns(stream);

        var request = new PreviewMappingRequest(JsonDocument.Parse("{}").RootElement);

        // Act
        var result = await sut.PreviewMappings(templateId, request, CancellationToken.None);

        // Assert
        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        sut.Response.Headers.ContentDisposition.ToString().Should().Be("inline");
    }
}
