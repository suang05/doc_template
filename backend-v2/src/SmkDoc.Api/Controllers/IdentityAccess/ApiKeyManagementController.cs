using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Extensions;
using SmkDoc.Api.Models;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

namespace SmkDoc.Api.Controllers.IdentityAccess;

/// <summary>
/// API Key lifecycle management within a project.
/// Auth: JWT Bearer (JwtPolicy) — Admin role required for all operations.
/// </summary>
[ApiController]
[Route("api/v1/management/projects/{projectId:guid}/api-keys")]
[Authorize(Policy = "JwtPolicy")]
public class ApiKeyManagementController(
    ListApiKeysUseCase listApiKeysUseCase,
    CreateApiKeyUseCase createApiKeyUseCase,
    RevokeApiKeyUseCase revokeApiKeyUseCase) : ControllerBase
{
    private readonly ListApiKeysUseCase _listApiKeysUseCase = listApiKeysUseCase;
    private readonly CreateApiKeyUseCase _createApiKeyUseCase = createApiKeyUseCase;
    private readonly RevokeApiKeyUseCase _revokeApiKeyUseCase = revokeApiKeyUseCase;

    /// <summary>List all API keys for the specified project. Admin only.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ApiKeyDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListKeys([FromRoute] Guid projectId, CancellationToken ct)
    {
        User.RequireAdmin();
        var keys = await _listApiKeysUseCase.ExecuteAsync(new ListApiKeysQuery(projectId), ct);
        return Ok(new ApiResponse<IEnumerable<ApiKeyDto>>(keys));
    }

    /// <summary>Create a new API key for the specified project. Admin only.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ApiKeyResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateKey(
        [FromRoute] Guid projectId,
        [FromBody] CreateApiKeyRequest request,
        CancellationToken ct)
    {
        User.RequireAdmin();
        var result = await _createApiKeyUseCase.ExecuteAsync(
            new CreateApiKeyCommand(request.Name, request.CallerApp, projectId),
            ct);

        var dto = new ApiKeyResponseDto(result.Id, result.Name, result.CallerApp, result.PlainTextKey, null);
        return Ok(new ApiResponse<ApiKeyResponseDto>(dto));
    }

    /// <summary>Revoke (permanently delete) an API key. Admin only.</summary>
    [HttpDelete("{keyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeKey(
        [FromRoute] Guid projectId,
        [FromRoute] Guid keyId,
        CancellationToken ct)
    {
        User.RequireAdmin();
        await _revokeApiKeyUseCase.ExecuteAsync(new RevokeApiKeyCommand(keyId), ct);
        return NoContent();
    }
}
