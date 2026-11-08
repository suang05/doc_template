using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.FieldMappings;
using SmkDoc.Api.Controllers.Authoring;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class TemplateMappingControllerTests
{
    private readonly IUseCase<GetTemplateMappingsQuery, List<FieldMappingDto>> _getMappingsUseCase = Substitute.For<IUseCase<GetTemplateMappingsQuery, List<FieldMappingDto>>>();
    private readonly IUseCase<SaveTemplateMappingsCommand, List<FieldMappingDto>> _saveMappingsUseCase = Substitute.For<IUseCase<SaveTemplateMappingsCommand, List<FieldMappingDto>>>();
    private readonly IUseCase<PreviewMappingQuery, Stream> _previewMappingUseCase = Substitute.For<IUseCase<PreviewMappingQuery, Stream>>();

    private TemplateMappingController CreateSut()
    {
        var controller = new TemplateMappingController(
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
        var result = await sut.SaveMappings(templateId, request, CancellationToken.None);

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

