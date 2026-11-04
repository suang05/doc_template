using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Api.Controllers.Authoring;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanTemplatePlaceholders;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanUploadedTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class TemplateControllerTests
{
    private readonly IUseCase<ListTemplatesQuery, TemplatePagedResultDto> _listTemplatesUseCase = Substitute.For<IUseCase<ListTemplatesQuery, TemplatePagedResultDto>>();
    private readonly IUseCase<GetTemplateByIdQuery, TemplateResultDto> _getTemplateByIdUseCase = Substitute.For<IUseCase<GetTemplateByIdQuery, TemplateResultDto>>();
    private readonly IUseCase<CreateTemplateCommand, TemplateResultDto> _createTemplateUseCase = Substitute.For<IUseCase<CreateTemplateCommand, TemplateResultDto>>();
    private readonly IUseCase<UpdateTemplateDetailsCommand, TemplateResultDto> _updateTemplateDetailsUseCase = Substitute.For<IUseCase<UpdateTemplateDetailsCommand, TemplateResultDto>>();
    private readonly IUseCase<DeactivateTemplateCommand> _deactivateTemplateUseCase = Substitute.For<IUseCase<DeactivateTemplateCommand>>();
    private readonly IUseCase<DownloadTemplateQuery, DownloadTemplateResult> _downloadTemplateUseCase = Substitute.For<IUseCase<DownloadTemplateQuery, DownloadTemplateResult>>();
    private readonly IUseCase<ValidateTemplatePayloadQuery, ValidateTemplatePayloadResult> _validatePayloadUseCase = Substitute.For<IUseCase<ValidateTemplatePayloadQuery, ValidateTemplatePayloadResult>>();
    private readonly IUseCase<ScanUploadedTemplateQuery, ScanFieldsResultDto> _scanUploadedUseCase = Substitute.For<IUseCase<ScanUploadedTemplateQuery, ScanFieldsResultDto>>();
    private readonly IUseCase<ScanTemplatePlaceholdersQuery, ScanFieldsResultDto> _scanStoredUseCase = Substitute.For<IUseCase<ScanTemplatePlaceholdersQuery, ScanFieldsResultDto>>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    private TemplateController CreateSut()
    {
        var controller = new TemplateController(
            _listTemplatesUseCase,
            _getTemplateByIdUseCase,
            _createTemplateUseCase,
            _updateTemplateDetailsUseCase,
            _deactivateTemplateUseCase,
            _downloadTemplateUseCase,
            _validatePayloadUseCase,
            _scanUploadedUseCase,
            _scanStoredUseCase,
            _projectContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    [Fact]
    public async Task ScanFieldsStateless_WithValidFile_ShouldReturnPlaceholders()
    {
        // Arrange
        var sut = CreateSut();
        var formFile = Substitute.For<IFormFile>();
        formFile.Length.Returns(100);
        formFile.FileName.Returns("template.docx");
        formFile.OpenReadStream().Returns(new MemoryStream([1, 2, 3]));

        _scanUploadedUseCase.ExecuteAsync(Arg.Any<ScanUploadedTemplateQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ScanFieldsResultDto(["customerName", "totalAmount"]));

        // Act
        var result = await sut.ScanFieldsStateless(formFile, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<ScanFieldsResultDto>>().Subject;
        body.Data.Placeholders.Should().Contain("customerName");
        body.Data.Placeholders.Should().Contain("totalAmount");
    }

    [Fact]
    public async Task ScanFieldsStateless_WithEmptyFile_ShouldThrowDomainValidationException()
    {
        // Arrange
        var sut = CreateSut();
        var formFile = Substitute.For<IFormFile>();
        formFile.Length.Returns(0);

        // Act
        var act = () => sut.ScanFieldsStateless(formFile, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task ScanFieldsById_ShouldReturnPlaceholdersForStoredTemplate()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        _scanStoredUseCase.ExecuteAsync(Arg.Is<ScanTemplatePlaceholdersQuery>(q => q.Id == templateId), Arg.Any<CancellationToken>())
            .Returns(new ScanFieldsResultDto(["invoiceDate", "lineItems"]));

        // Act
        var result = await sut.ScanFieldsById(templateId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<ScanFieldsResultDto>>().Subject;
        body.Data.Placeholders.Should().Contain("invoiceDate");
    }

    [Fact]
    public async Task UpdateMetadata_WithIfMatch_ShouldPassIfMatchAndSetETagHeader()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var token = now.ToUnixTimeMilliseconds().ToString();
        var resultDto = new TemplateResultDto(
            templateId,
            Guid.NewGuid(),
            "New Template Name",
            "new-template-name",
            "Billing",
            true,
            null,
            "html",
            now.AddDays(-1),
            now,
            token);

        _updateTemplateDetailsUseCase.ExecuteAsync(
            Arg.Is<UpdateTemplateDetailsCommand>(c => c.TemplateId == templateId && c.IfMatch == "\"12345\""),
            Arg.Any<CancellationToken>())
            .Returns(resultDto);

        var request = new UpdateTemplateMetadataRequest("New Template Name", "Billing");

        // Act
        var result = await sut.UpdateMetadata(templateId, request, "\"12345\"", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<TemplateResultDto>>().Subject;
        body.Data.Name.Should().Be("New Template Name");
        sut.Response.Headers.ETag.ToString().Should().Be($"\"{now.ToUnixTimeMilliseconds()}\"");
    }

    [Fact]
    public async Task GetById_ShouldSetETagHeader()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var token = now.ToUnixTimeMilliseconds().ToString();
        var resultDto = new TemplateResultDto(
            templateId,
            Guid.NewGuid(),
            "Template Name",
            "template-name",
            "Billing",
            true,
            null,
            "html",
            now,
            null,
            token);

        _getTemplateByIdUseCase.ExecuteAsync(Arg.Is<GetTemplateByIdQuery>(q => q.Id == templateId), Arg.Any<CancellationToken>())
            .Returns(resultDto);

        // Act
        var result = await sut.GetById(templateId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        sut.Response.Headers.ETag.ToString().Should().Be($"\"{now.ToUnixTimeMilliseconds()}\"");
    }
}

