using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Api.Controllers.Authoring;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateHtmlSource;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateSchema;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateStudioBundle;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class TemplateStudioControllerTests
{
    private readonly IUseCase<GetTemplateHtmlSourceQuery, string> _getHtmlSourceUseCase = Substitute.For<IUseCase<GetTemplateHtmlSourceQuery, string>>();
    private readonly IUseCase<GetTemplateStudioBundleQuery, TemplateStudioDto> _getStudioBundleUseCase = Substitute.For<IUseCase<GetTemplateStudioBundleQuery, TemplateStudioDto>>();
    private readonly IUseCase<GetTemplateSchemaQuery, TemplateSchemaDto> _getSchemaUseCase = Substitute.For<IUseCase<GetTemplateSchemaQuery, TemplateSchemaDto>>();
    private readonly IUseCase<SaveTemplateHtmlCommand, SaveHtmlResultDto> _saveHtmlUseCase = Substitute.For<IUseCase<SaveTemplateHtmlCommand, SaveHtmlResultDto>>();
    private readonly IUseCase<ValidateTemplateHtmlQuery, TemplateValidationResultDto> _validateUseCase = Substitute.For<IUseCase<ValidateTemplateHtmlQuery, TemplateValidationResultDto>>();

    private TemplateStudioController CreateSut()
    {
        var controller = new TemplateStudioController(
            _getHtmlSourceUseCase,
            _getStudioBundleUseCase,
            _getSchemaUseCase,
            _saveHtmlUseCase,
            _validateUseCase)
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
    public async Task GetSchema_ShouldReturnSchemaEnvelope()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        var schemaDto = new TemplateSchemaDto(templateId, "invoice", TemplateFormat.Html, 1, "{\"type\":\"object\"}", "{}");

        _getSchemaUseCase.ExecuteAsync(Arg.Is<GetTemplateSchemaQuery>(q => q.TemplateId == templateId), Arg.Any<CancellationToken>())
            .Returns(schemaDto);

        // Act
        var result = await sut.GetSchema(templateId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<TemplateSchemaDto>>().Subject;
        body.Data.DataSchema.Should().Be("{\"type\":\"object\"}");
    }

    [Fact]
    public async Task SaveHtml_ShouldExecuteCommandAndReturnOk()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        _saveHtmlUseCase.ExecuteAsync(Arg.Is<SaveTemplateHtmlCommand>(c => c.TemplateId == templateId), Arg.Any<CancellationToken>())
            .Returns(new SaveHtmlResultDto(2));

        var request = new SaveHtmlRequest("<html>updated</html>", "{}", "Increment note");

        // Act
        var result = await sut.SaveHtml(templateId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<SaveHtmlResultDto>>().Subject;
        body.Data.Version.Should().Be(2);
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
}
