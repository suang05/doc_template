using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Authoring.FieldMappings;
using SmkDoc.Api.Controllers.Authoring;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class TemplateMappingControllerTests
{
    private readonly IUseCase<GetTemplateMappingsQuery, List<FieldMappingResultDto>> _getMappingsUseCase = Substitute.For<IUseCase<GetTemplateMappingsQuery, List<FieldMappingResultDto>>>();
    private readonly IUseCase<SaveTemplateMappingsCommand, List<FieldMappingResultDto>> _saveMappingsUseCase = Substitute.For<IUseCase<SaveTemplateMappingsCommand, List<FieldMappingResultDto>>>();
    private readonly IUseCase<PreviewDocumentQuery, Stream> _previewDocumentUseCase = Substitute.For<IUseCase<PreviewDocumentQuery, Stream>>();

    [Fact]
    public void Controller_ShouldBeDecoratedWithTemplateManagementPolicy()
    {
        var type = typeof(TemplateMappingController);

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Policy.Should().Be(AuthorizationPolicies.CanManageTemplates);
    }

    private TemplateMappingController CreateSut()
    {
        var controller = new TemplateMappingController(
            _getMappingsUseCase,
            _saveMappingsUseCase,
            _previewDocumentUseCase)
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
        var mappings = new List<FieldMappingResultDto>
        {
            new(Guid.NewGuid(), templateId, "company", "req.company", "Company", false, null, null, 1, "direct", null, null, null)
        };
        _getMappingsUseCase.ExecuteAsync(Arg.Is<GetTemplateMappingsQuery>(q => q.TemplateId == templateId), Arg.Any<CancellationToken>())
            .Returns(mappings);

        // Act
        var result = await sut.GetMappings(templateId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<FieldMappingResultDto>>>().Subject;
        body.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task SaveMappings_ShouldExecuteCommandAndReturnUpdatedMappings()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        var mappings = new List<FieldMappingResultDto>
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
        var body = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<FieldMappingResultDto>>>().Subject;
        body.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task PreviewMappings_ShouldReturnInlinePdfStream()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        using var stream = new MemoryStream([1, 2, 3]);
        _previewDocumentUseCase.ExecuteAsync(Arg.Is<PreviewDocumentQuery>(q => q.TemplateId == templateId), Arg.Any<CancellationToken>())
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

