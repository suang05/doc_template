using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.IdentityAccess.ApiKeys;
using SmkDoc.Api.Controllers.IdentityAccess;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class ApiKeysControllerTests
{
    private readonly IUseCase<ListApiKeysQuery, List<ApiKeyResultDto>> _listUseCase = Substitute.For<IUseCase<ListApiKeysQuery, List<ApiKeyResultDto>>>();
    private readonly IUseCase<CreateApiKeyCommand, CreateApiKeyResultDto> _createUseCase = Substitute.For<IUseCase<CreateApiKeyCommand, CreateApiKeyResultDto>>();
    private readonly IUseCase<RevokeApiKeyCommand> _revokeUseCase = Substitute.For<IUseCase<RevokeApiKeyCommand>>();

    private ApiKeysController CreateSut() =>
        new(_listUseCase, _createUseCase, _revokeUseCase);

    [Fact]
    public async Task ListKeys_CallsUseCaseWithAmbientProjectQuery_ReturnsOk()
    {
        // Arrange
        var keys = new List<ApiKeyResultDto>
        {
            new(Guid.NewGuid(), "CRM Integration", "crm", true, null, DateTimeOffset.UtcNow, "ReadWrite", "smk_crm_****abcd")
        };
        _listUseCase.ExecuteAsync(Arg.Any<ListApiKeysQuery>(), Arg.Any<CancellationToken>())
            .Returns(keys);

        var controller = CreateSut();

        // Act
        var result = await controller.ListKeys(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<ApiKeyResultDto>>>().Subject;
        response.Data.Should().HaveCount(1);
        await _listUseCase.Received(1).ExecuteAsync(
            Arg.Is<ListApiKeysQuery>(q => !q.ProjectId.HasValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateKey_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var commandResult = new CreateApiKeyResultDto(
            Guid.NewGuid(),
            "ERP Key",
            "erp",
            "smk_erp_plain_secret_12345");

        _createUseCase.ExecuteAsync(Arg.Any<CreateApiKeyCommand>(), Arg.Any<CancellationToken>())
            .Returns(commandResult);

        var controller = CreateSut();
        var request = new CreateApiKeyRequest("ERP Key", "erp", "ReadWrite");

        // Act
        var result = await controller.CreateKey(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        var response = createdResult.Value.Should().BeOfType<ApiResponse<CreateApiKeyResultDto>>().Subject;
        response.Data.Key.Should().Be("smk_erp_plain_secret_12345");
        response.Data.Scope.Should().Be("ReadWrite");
    }

    [Fact]
    public async Task RevokeKey_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var keyId = Guid.NewGuid();
        _revokeUseCase.ExecuteAsync(Arg.Any<RevokeApiKeyCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = CreateSut();

        // Act
        var result = await controller.RevokeKey(keyId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        await _revokeUseCase.Received(1).ExecuteAsync(
            Arg.Is<RevokeApiKeyCommand>(c => c.Id == keyId),
            Arg.Any<CancellationToken>());
    }
}

