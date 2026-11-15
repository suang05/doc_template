using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Api.Controllers.Authoring;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class TemplateLifecycleControllerTests
{
    private readonly IUseCase<ListTemplatesQuery, TemplatePagedResultDto> _listTemplatesUseCase = Substitute.For<IUseCase<ListTemplatesQuery, TemplatePagedResultDto>>();
    private readonly IUseCase<GetTemplateByIdQuery, TemplateResultDto> _getTemplateByIdUseCase = Substitute.For<IUseCase<GetTemplateByIdQuery, TemplateResultDto>>();
    private readonly IUseCase<CreateTemplateCommand, TemplateResultDto> _createTemplateUseCase = Substitute.For<IUseCase<CreateTemplateCommand, TemplateResultDto>>();
    private readonly IUseCase<UpdateTemplateDetailsCommand, TemplateResultDto> _updateTemplateDetailsUseCase = Substitute.For<IUseCase<UpdateTemplateDetailsCommand, TemplateResultDto>>();
    private readonly IUseCase<DeactivateTemplateCommand> _deactivateTemplateUseCase = Substitute.For<IUseCase<DeactivateTemplateCommand>>();

    private TemplateLifecycleController CreateSut()
    {
        var controller = new TemplateLifecycleController(
            _listTemplatesUseCase,
            _getTemplateByIdUseCase,
            _createTemplateUseCase,
            _updateTemplateDetailsUseCase,
            _deactivateTemplateUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    [Fact]
    public async Task ListTemplates_WhenValid_ShouldDispatchQueryAndReturnPagedResponse()
    {
        // Arrange
        var sut = CreateSut();
        var pagedResult = new TemplatePagedResultDto([], null, 1, 20);
        _listTemplatesUseCase.ExecuteAsync(Arg.Any<ListTemplatesQuery>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await sut.ListTemplates("search", "cat", true, 1, 20, false, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = okResult.Value.Should().BeOfType<PagedApiResponse<TemplateResultDto>>().Subject;
        paged.Pagination.Page.Should().Be(1);
        paged.Pagination.Limit.Should().Be(20);
        paged.Pagination.TotalCount.Should().BeNull();

        await _listTemplatesUseCase.Received(1).ExecuteAsync(
            Arg.Is<ListTemplatesQuery>(q =>
                q.SearchTerm == "search" &&
                q.Category == "cat" &&
                q.IsActive == true &&
                q.Page == 1 &&
                q.Limit == 20 &&
                !q.IncludeTotal),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_ShouldReturnOkWithTemplate()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
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
            1L);

        _getTemplateByIdUseCase.ExecuteAsync(Arg.Is<GetTemplateByIdQuery>(q => q.Id == templateId), Arg.Any<CancellationToken>())
            .Returns(resultDto);

        // Act
        var result = await sut.GetById(templateId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<TemplateResultDto>>().Subject;
        body.Data.Id.Should().Be(templateId);
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
            1L);

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
    public async Task UpdateMetadata_ShouldExecuteCommandAndReturnOk()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
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
            2L);

        _updateTemplateDetailsUseCase.ExecuteAsync(
            Arg.Is<UpdateTemplateDetailsCommand>(c => c.TemplateId == templateId && c.ExpectedVersion == 1L),
            Arg.Any<CancellationToken>())
            .Returns(resultDto);

        var request = new UpdateTemplateMetadataRequest("New Template Name", "Billing");

        // Act
        var result = await sut.UpdateMetadata(templateId, 1L, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<TemplateResultDto>>().Subject;
        body.Data.Name.Should().Be("New Template Name");
    }

    [Fact]
    public async Task Deactivate_ShouldExecuteCommandAndReturnNoContent()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();

        // Act
        var result = await sut.Deactivate(templateId, 1L, CancellationToken.None);

        // Assert
        await _deactivateTemplateUseCase.Received(1).ExecuteAsync(
            Arg.Is<DeactivateTemplateCommand>(c => c.TemplateId == templateId && c.ExpectedVersion == 1L),
            Arg.Any<CancellationToken>());
        result.Should().BeOfType<NoContentResult>();
    }
}
