using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Api.Controllers.Authoring;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class TemplateLifecycleControllerTests
{
    private readonly IUseCase<ListTemplatesQuery, TemplatePagedResultDto> _listTemplatesUseCase = Substitute.For<IUseCase<ListTemplatesQuery, TemplatePagedResultDto>>();
    private readonly IUseCase<GetTemplateByIdQuery, TemplateResultDto> _getTemplateByIdUseCase = Substitute.For<IUseCase<GetTemplateByIdQuery, TemplateResultDto>>();
    private readonly IUseCase<CreateTemplateCommand, TemplateResultDto> _createTemplateUseCase = Substitute.For<IUseCase<CreateTemplateCommand, TemplateResultDto>>();
    private readonly IUseCase<UpdateTemplateDetailsCommand, TemplateResultDto> _updateTemplateDetailsUseCase = Substitute.For<IUseCase<UpdateTemplateDetailsCommand, TemplateResultDto>>();
    private readonly IUseCase<DeactivateTemplateCommand> _deactivateTemplateUseCase = Substitute.For<IUseCase<DeactivateTemplateCommand>>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    private TemplateLifecycleController CreateSut()
    {
        var controller = new TemplateLifecycleController(
            _listTemplatesUseCase,
            _getTemplateByIdUseCase,
            _createTemplateUseCase,
            _updateTemplateDetailsUseCase,
            _deactivateTemplateUseCase,
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
    public async Task ListTemplates_WhenContextNotResolved_ShouldThrowForbiddenException()
    {
        // Arrange
        var sut = CreateSut();
        _projectContext.IsResolved.Returns(false);

        // Act
        var act = () => sut.ListTemplates(null, null, null, null, 1, 20, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Active project context is required*");
    }

    [Fact]
    public async Task ListTemplates_WhenProjectIdDoesNotMatchContext_ShouldThrowForbiddenException()
    {
        // Arrange
        var sut = CreateSut();
        var contextProjectId = Guid.NewGuid();
        var callerProjectId = Guid.NewGuid();
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(contextProjectId));

        // Act
        var act = () => sut.ListTemplates(callerProjectId, null, null, null, 1, 20, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*does not match the active project context*");
    }

    [Fact]
    public async Task ListTemplates_WhenValid_ShouldReturnPagedResponse()
    {
        // Arrange
        var sut = CreateSut();
        var contextProjectId = Guid.NewGuid();
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(contextProjectId));

        var pagedResult = new TemplatePagedResultDto([], 0, 1, 20);
        _listTemplatesUseCase.ExecuteAsync(Arg.Any<ListTemplatesQuery>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await sut.ListTemplates(contextProjectId, null, null, null, 1, 20, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = okResult.Value.Should().BeOfType<PagedApiResponse<TemplateResultDto>>().Subject;
        paged.Pagination.Page.Should().Be(1);
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

    [Fact]
    public async Task CreateTemplate_ShouldReturnCreatedAtAction()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var resultDto = new TemplateResultDto(
            templateId,
            Guid.NewGuid(),
            "Invoice",
            "invoice",
            "Billing",
            true,
            null,
            "html",
            now,
            null,
            "123");

        _createTemplateUseCase.ExecuteAsync(Arg.Any<CreateTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(resultDto);

        // Act
        var result = await sut.CreateTemplate("Invoice", "invoice", "Billing", null, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(TemplateLifecycleController.GetById));
        var body = createdResult.Value.Should().BeOfType<ApiResponse<TemplateResultDto>>().Subject;
        body.Data.Id.Should().Be(templateId);
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
    public async Task Deactivate_ShouldExecuteCommandAndReturnNoContent()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();

        // Act
        var result = await sut.Deactivate(templateId, null, CancellationToken.None);

        // Assert
        await _deactivateTemplateUseCase.Received(1).ExecuteAsync(
            Arg.Is<DeactivateTemplateCommand>(c => c.TemplateId == templateId),
            Arg.Any<CancellationToken>());
        result.Should().BeOfType<NoContentResult>();
    }
}
