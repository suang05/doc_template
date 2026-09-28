using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Models;
using SmkDoc.Application.DTOs.Security;
using SmkDoc.Application.UseCases.Security;
using SmkDoc.Api.Extensions;

namespace SmkDoc.Api.Controllers;

/// <summary>
/// API Key lifecycle management within a project.
/// Auth: JWT Bearer (JwtPolicy) — Admin role required for all operations.
/// </summary>
[ApiController]
[Route("api/v1/management/projects/{projectId:guid}/api-keys")]
[Authorize(Policy = "JwtPolicy")]
public class ApiKeyManagementController(ApiKeyUseCase apiKeyUseCase) : ControllerBase
{
    /// <summary>List all API keys for the specified project. Admin only.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ApiKeyDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListKeys([FromRoute] Guid projectId, CancellationToken ct)
    {
        User.RequireAdmin();
        var keys = await apiKeyUseCase.ListKeysAsync(ct);
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
        var result = await apiKeyUseCase.CreateKeyAsync(
            request.Name,
            request.CallerApp,
            projectId,
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
        await apiKeyUseCase.RevokeKeyAsync(keyId, ct);
        return NoContent();
    }
}
