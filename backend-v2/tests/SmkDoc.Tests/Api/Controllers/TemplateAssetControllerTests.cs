using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Controllers.Authoring;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanTemplatePlaceholders;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class TemplateAssetControllerTests
{
    private readonly IUseCase<DownloadTemplateQuery, DownloadTemplateResult> _downloadTemplateUseCase = Substitute.For<IUseCase<DownloadTemplateQuery, DownloadTemplateResult>>();
    private readonly IUseCase<ScanTemplatePlaceholdersQuery, ScanFieldsResultDto> _scanStoredUseCase = Substitute.For<IUseCase<ScanTemplatePlaceholdersQuery, ScanFieldsResultDto>>();
    private readonly IUseCase<ValidateTemplatePayloadQuery, ValidateTemplatePayloadResult> _validatePayloadUseCase = Substitute.For<IUseCase<ValidateTemplatePayloadQuery, ValidateTemplatePayloadResult>>();

    private TemplateAssetController CreateSut()
    {
        var controller = new TemplateAssetController(
            _downloadTemplateUseCase,
            _scanStoredUseCase,
            _validatePayloadUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    [Fact]
    public async Task Download_ShouldReturnFileResult()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        using var stream = new MemoryStream([1, 2, 3]);
        var downloadResult = new DownloadTemplateResult(stream, "application/pdf", "invoice.pdf");

        _downloadTemplateUseCase.ExecuteAsync(Arg.Is<DownloadTemplateQuery>(q => q.Id == templateId), Arg.Any<CancellationToken>())
            .Returns(downloadResult);

        // Act
        var result = await sut.Download(templateId, CancellationToken.None);

        // Assert
        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be("invoice.pdf");
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
    public async Task ValidatePayload_WithValidData_ShouldReturnValidationResult()
    {
        // Arrange
        var sut = CreateSut();
        var json = JsonDocument.Parse("{\"customerName\": \"John\"}").RootElement;
        var validationResult = new ValidateTemplatePayloadResult(true, "invoice", 1, "Payload is valid.");

        _validatePayloadUseCase.ExecuteAsync(Arg.Is<ValidateTemplatePayloadQuery>(q => q.Slug == "invoice"), Arg.Any<CancellationToken>())
            .Returns(validationResult);

        // Act
        var result = await sut.ValidatePayload("invoice", json, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<ValidateTemplatePayloadResult>>().Subject;
        body.Data.Valid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePayload_WithNullOrUndefinedData_ShouldDelegateToUseCase()
    {
        // Arrange
        var sut = CreateSut();
        var defaultElement = default(JsonElement);
        _validatePayloadUseCase.ExecuteAsync(Arg.Any<ValidateTemplatePayloadQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainValidationException("Request payload body cannot be null or empty."));

        // Act
        var act = () => sut.ValidatePayload("invoice", defaultElement, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>();
        await _validatePayloadUseCase.Received(1).ExecuteAsync(
            Arg.Is<ValidateTemplatePayloadQuery>(q => q.Slug == "invoice" && q.Data.ValueKind == JsonValueKind.Undefined),
            Arg.Any<CancellationToken>());
    }
}
