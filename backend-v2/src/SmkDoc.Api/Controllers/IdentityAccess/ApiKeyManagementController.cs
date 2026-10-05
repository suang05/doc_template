using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.IdentityAccess.ApiKeys;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

namespace SmkDoc.Api.Controllers.IdentityAccess;

/// <summary>
/// API Key lifecycle management within a project.
/// Auth: JWT Bearer — Admin role required for all operations.
/// </summary>
[ApiController]
[Route("api/v1/management/projects/{projectId:guid}/api-keys")]
[Authorize(Roles = "Admin")] // ใช้ Declarative Authorization ระดับ Controller ป้องกันข้อผิดพลาดแบบ Fail-Safe
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class ApiKeyManagementController(
    ListApiKeysUseCase listApiKeysUseCase,
    CreateApiKeyUseCase createApiKeyUseCase,
    RevokeApiKeyUseCase revokeApiKeyUseCase) : ControllerBase
{
    /// <summary>List all API keys for the specified project. Admin only.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ApiKeyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListKeys([FromRoute] Guid projectId, CancellationToken ct)
    {
        var query = new ListApiKeysQuery(projectId);
        var keys = await listApiKeysUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<ApiKeyDto>>(keys));
    }

    /// <summary>Create a new API key for the specified project. Admin only.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ApiKeyResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateKey(
        [FromRoute] Guid projectId,
        [FromBody] CreateApiKeyRequest request,
        CancellationToken ct)
    {
        var command = new CreateApiKeyCommand(request.Name, request.CallerApp, projectId);
        var result = await createApiKeyUseCase.ExecuteAsync(command, ct);

        var dto = new ApiKeyResponseDto(
            result.Id,
            result.Name,
            result.CallerApp,
            result.PlainTextKey,
            request.ExpiresAt);

        // คืน 201 Created ตามมาตรฐาน RESTful
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<ApiKeyResponseDto>(dto));
    }

    /// <summary>Revoke (permanently delete) an API key. Admin only.</summary>
    [HttpDelete("{keyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeKey(
        [FromRoute] Guid projectId,
        [FromRoute] Guid keyId,
        CancellationToken ct)
    {
        var command = new RevokeApiKeyCommand(keyId, projectId);
        await revokeApiKeyUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }
}