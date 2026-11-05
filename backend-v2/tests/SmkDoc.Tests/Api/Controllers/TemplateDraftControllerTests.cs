using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Api.Controllers.Authoring;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateDraft;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class TemplateDraftControllerTests
{
    private readonly IUseCase<ParseTemplateDraftCommand, ParseDraftResultDto> _parseUseCase =
        Substitute.For<IUseCase<ParseTemplateDraftCommand, ParseDraftResultDto>>();
    private readonly IUseCase<PreviewTemplateDraftQuery, Stream> _previewUseCase =
        Substitute.For<IUseCase<PreviewTemplateDraftQuery, Stream>>();
    private readonly IUseCase<CommitTemplateDraftCommand, CommitDraftResultDto> _commitUseCase =
        Substitute.For<IUseCase<CommitTemplateDraftCommand, CommitDraftResultDto>>();

    private TemplateDraftController CreateSut()
    {
        return new TemplateDraftController(
            _parseUseCase,
            _previewUseCase,
            _commitUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void TemplateDraftController_ShouldBeDecoratedWithSecurityAndRoutingAttributes()
    {
        var type = typeof(TemplateDraftController);

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Policy.Should().Be(SmkDocPolicies.CanManageTemplates);

        var routes = type.GetCustomAttributes<RouteAttribute>().Select(r => r.Template).ToList();
        routes.Should().ContainSingle().Which.Should().Be("api/v1/templates/draft");
    }

    [Fact]
    public async Task ParseDraft_WhenFileProvided_ShouldReturnOkWithParseDraftResult()
    {
        // Arrange
        var sut = CreateSut();
        var expectedResult = new ParseDraftResultDto("draft-123", ["customer.name", "total"]);
        _parseUseCase.ExecuteAsync(Arg.Any<ParseTemplateDraftCommand>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var file = Substitute.For<IFormFile>();
        var content = Encoding.UTF8.GetBytes("fake file content");
        var stream = new MemoryStream(content);
        file.Length.Returns(content.Length);
        file.FileName.Returns("invoice.docx");
        file.OpenReadStream().Returns(stream);

        // Act
        var result = await sut.ParseDraft(file, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<ParseDraftResultDto>>().Subject;
        apiResponse.Data.DraftId.Should().Be("draft-123");
        apiResponse.Data.Placeholders.Should().BeEquivalentTo(new[] { "customer.name", "total" });
    }

    [Fact]
    public async Task ParseDraft_WhenFileEmpty_ShouldThrowDomainValidationException()
    {
        // Arrange
        var sut = CreateSut();
        var file = Substitute.For<IFormFile>();
        file.Length.Returns(0);

        // Act
        var act = () => sut.ParseDraft(file, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*File is required*");
    }

    [Fact]
    public async Task PreviewDraft_WhenDraftValid_ShouldReturnFileResult()
    {
        // Arrange
        var sut = CreateSut();
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Preview Content");
        var pdfStream = new MemoryStream(pdfBytes);
        _previewUseCase.ExecuteAsync(Arg.Is<PreviewTemplateDraftQuery>(q => q.DraftId == "draft-123"), Arg.Any<CancellationToken>())
            .Returns(pdfStream);

        var request = new PreviewDraftRequest("{\"name\":\"Test\"}");

        // Act
        var result = await sut.PreviewDraft("draft-123", request, CancellationToken.None);

        // Assert
        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        sut.Response.Headers.ContentDisposition.ToString().Should().Be("inline");
    }

    [Fact]
    public async Task CommitDraft_WhenValidRequest_ShouldReturnCreatedAtAction()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        _commitUseCase.ExecuteAsync(Arg.Any<CommitTemplateDraftCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommitDraftResultDto(templateId));

        var request = new CommitDraftRequest(
            Name: "Tax Invoice",
            Slug: "tax-invoice",
            Category: "Finance",
            Mappings: []);

        // Act
        var result = await sut.CommitDraft("draft-123", request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var apiResponse = createdResult.Value.Should().BeOfType<ApiResponse<CommitDraftResultDto>>().Subject;
        apiResponse.Data.TemplateId.Should().Be(templateId);
    }
}

