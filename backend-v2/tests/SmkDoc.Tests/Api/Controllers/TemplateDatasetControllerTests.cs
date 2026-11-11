using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Api.Controllers.Authoring;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateDatasets;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class TemplateDatasetControllerTests
{
    private readonly IUseCase<GetTemplateDatasetsQuery, List<TemplateDatasetResultDto>> _getDatasetsUseCase = Substitute.For<IUseCase<GetTemplateDatasetsQuery, List<TemplateDatasetResultDto>>>();
    private readonly IUseCase<SaveTemplateDatasetsCommand, List<TemplateDatasetResultDto>> _saveDatasetsUseCase = Substitute.For<IUseCase<SaveTemplateDatasetsCommand, List<TemplateDatasetResultDto>>>();

    [Fact]
    public void Controller_ShouldBeDecoratedWithTemplateManagementPolicy()
    {
        var type = typeof(TemplateDatasetController);

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Policy.Should().Be(AuthorizationPolicies.CanManageTemplates);
    }

    private TemplateDatasetController CreateSut()
    {
        var controller = new TemplateDatasetController(_getDatasetsUseCase, _saveDatasetsUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    [Fact]
    public async Task GetDatasets_ShouldReturnDatasetsList()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        var datasets = new List<TemplateDatasetResultDto>
        {
            new(Guid.NewGuid(), templateId, Guid.NewGuid(), "CustData", "Customer Dataset", 1)
        };

        _getDatasetsUseCase.ExecuteAsync(Arg.Is<GetTemplateDatasetsQuery>(q => q.TemplateId == templateId), Arg.Any<CancellationToken>())
            .Returns(datasets);

        // Act
        var result = await sut.GetDatasets(templateId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<TemplateDatasetResultDto>>>().Subject;
        body.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task SaveDatasets_ShouldExecuteCommandAndReturnUpdatedList()
    {
        // Arrange
        var sut = CreateSut();
        var templateId = Guid.NewGuid();
        var datasetId = Guid.NewGuid();
        var datasets = new List<TemplateDatasetResultDto>
        {
            new(Guid.NewGuid(), templateId, datasetId, "CustData", "Customer Dataset", 1)
        };

        _saveDatasetsUseCase.ExecuteAsync(Arg.Is<SaveTemplateDatasetsCommand>(c => c.TemplateId == templateId), Arg.Any<CancellationToken>())
            .Returns(datasets);

        var request = new SaveTemplateDatasetsRequest([
            new SaveTemplateDatasetItemRequest(datasetId, "CustData", 1)
        ]);

        // Act
        var result = await sut.SaveDatasets(templateId, request, CancellationToken.None);

        // Assert
        await _saveDatasetsUseCase.Received(1).ExecuteAsync(Arg.Is<SaveTemplateDatasetsCommand>(c => c.TemplateId == templateId), Arg.Any<CancellationToken>());
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<TemplateDatasetResultDto>>>().Subject;
        body.Data.Should().HaveCount(1);
    }
}

