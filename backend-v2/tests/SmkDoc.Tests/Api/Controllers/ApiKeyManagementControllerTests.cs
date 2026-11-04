using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.IdentityAccess.ApiKeys;
using SmkDoc.Api.Controllers.IdentityAccess;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class ApiKeyManagementControllerTests
{
    private readonly IUseCase<ListApiKeysQuery, List<ApiKeyDto>> _listUseCase = Substitute.For<IUseCase<ListApiKeysQuery, List<ApiKeyDto>>>();
    private readonly IUseCase<CreateApiKeyCommand, CreateApiKeyResultDto> _createUseCase = Substitute.For<IUseCase<CreateApiKeyCommand, CreateApiKeyResultDto>>();
    private readonly IUseCase<RevokeApiKeyCommand> _revokeUseCase = Substitute.For<IUseCase<RevokeApiKeyCommand>>();

    private ApiKeyManagementController CreateSut() =>
        new(_listUseCase, _createUseCase, _revokeUseCase);

    [Fact]
    public async Task ListKeys_WithProjectId_InvokesUseCaseAndReturnsOk()
    {
        // Arrange
        var projId = Guid.NewGuid();
        _listUseCase.ExecuteAsync(Arg.Any<ListApiKeysQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<ApiKeyDto>());

        var controller = CreateSut();

        // Act
        var result = await controller.ListKeys(projId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        await _listUseCase.Received(1).ExecuteAsync(
            Arg.Is<ListApiKeysQuery>(q => q.ProjectId == projId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateKey_WithValidRouteAndBody_InvokesUseCaseAndReturnsCreated()
    {
        // Arrange
        var projId = Guid.NewGuid();
        var commandResult = new CreateApiKeyResultDto(
            Guid.NewGuid(),
            "Admin Key",
            "mgmt",
            "smk_mgmt_plain_secret_123");

        _createUseCase.ExecuteAsync(Arg.Any<CreateApiKeyCommand>(), Arg.Any<CancellationToken>())
            .Returns(commandResult);

        var controller = CreateSut();
        var request = new CreateApiKeyRequest("Admin Key", "mgmt", "ReadWrite");

        // Act
        var result = await controller.CreateKey(projId, request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        var response = createdResult.Value.Should().BeOfType<ApiResponse<CreateApiKeyResultDto>>().Subject;
        response.Data.Key.Should().Be("smk_mgmt_plain_secret_123");
    }

    [Fact]
    public async Task RevokeKey_InvokesRevokeUseCase_ReturnsNoContent()
    {
        // Arrange
        var projId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        _revokeUseCase.ExecuteAsync(Arg.Any<RevokeApiKeyCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = CreateSut();

        // Act
        var result = await controller.RevokeKey(projId, keyId, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        await _revokeUseCase.Received(1).ExecuteAsync(
            Arg.Is<RevokeApiKeyCommand>(c => c.Id == keyId),
            Arg.Any<CancellationToken>());
    }
}
