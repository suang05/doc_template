using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.IdentityAccess.ApiKeys;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

namespace SmkDoc.Api.Controllers.IdentityAccess;

/// <summary>
/// API Key management endpoints for Portal UI.
/// Canonical multi-tenant endpoints are in <see cref="ApiKeyManagementController"/>.
/// Auth: Channel B (Bearer JWT).
/// </summary>
[ApiController]
[Route("api/v1/api-keys")]
[Route("api/api-keys")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class ApiKeyController(
    ListApiKeysUseCase listApiKeysUseCase,
    CreateApiKeyUseCase createApiKeyUseCase,
    RevokeApiKeyUseCase revokeApiKeyUseCase,
    ICurrentProjectContext projectContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ApiKeyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListKeys(
        [FromQuery] Guid? projectId,
        CancellationToken ct)
    {
        EnsureMatchingProjectContext(projectId);
        var keys = await listApiKeysUseCase.ExecuteAsync(new ListApiKeysQuery(), ct);
        return Ok(new ApiResponse<IEnumerable<ApiKeyDto>>(keys));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ApiKeyResponseDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateKey([FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        EnsureMatchingProjectContext(request.ProjectId);
        var command = new CreateApiKeyCommand(request.Name, request.CallerApp);
        var result = await createApiKeyUseCase.ExecuteAsync(command, ct);
        var dto = new ApiKeyResponseDto(
            result.Id,
            result.Name,
            result.CallerApp,
            result.PlainTextKey,
            null);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<ApiKeyResponseDto>(dto));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeKey(
        [FromRoute] Guid id,
        [FromQuery] Guid? projectId,
        CancellationToken ct)
    {
        EnsureMatchingProjectContext(projectId);
        var command = new RevokeApiKeyCommand(id);
        await revokeApiKeyUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }

    private void EnsureMatchingProjectContext(Guid? requestedProjectId)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Active project context is required for API key operations.");
        }

        if (requestedProjectId.HasValue && requestedProjectId.Value != Guid.Empty && requestedProjectId.Value != projectContext.ProjectId.Value)
        {
            throw new ForbiddenException("Access denied: requested projectId does not match the active project context.");
        }
    }
}
