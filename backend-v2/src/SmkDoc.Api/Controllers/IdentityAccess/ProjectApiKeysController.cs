using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.IdentityAccess.ApiKeys;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

namespace SmkDoc.Api.Controllers.IdentityAccess;

/// <summary>
/// Centralized SuperAdmin-only API Key lifecycle management within a project context.
/// Auth: JWT Bearer — SuperAdmin role strictly required for all operations.
/// </summary>
[ApiController]
[Route("api/v1/management/projects/{projectId:guid}/api-keys")]
[Authorize(Policy = AuthorizationPolicies.RequireSuperAdmin)]
[EnableRateLimiting("api")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class ProjectApiKeysController(
    IUseCase<ListApiKeysQuery, List<ApiKeyResultDto>> listApiKeysUseCase,
    IUseCase<CreateApiKeyCommand, CreateApiKeyResultDto> createApiKeyUseCase,
    IUseCase<RevokeApiKeyCommand> revokeApiKeyUseCase) : ControllerBase
{
    /// <summary>List all API keys for the specified project. SuperAdmin only.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ApiKeyResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListKeys(
        [FromRoute] Guid projectId,
        CancellationToken ct)
    {
        var query = new ListApiKeysQuery(projectId);
        var result = await listApiKeysUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<ApiKeyResultDto>>(result));
    }

    /// <summary>Issue a new API key for the specified project (Quota: Max 2 active keys). SuperAdmin only.</summary>
    [HttpPost]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<CreateApiKeyResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateKey(
        [FromRoute] Guid projectId,
        [FromBody] CreateApiKeyRequest request,
        CancellationToken ct)
    {
        var command = new CreateApiKeyCommand(request.Name, request.CallerApp, request.EffectiveScope);
        var result = await createApiKeyUseCase.ExecuteAsync(command, ct);
        return CreatedAtAction(nameof(ListKeys), new { projectId }, new ApiResponse<CreateApiKeyResultDto>(result));
    }

    /// <summary>Revoke an API key belonging to the specified project. SuperAdmin only.</summary>
    [HttpDelete("{keyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> RevokeKey(
        [FromRoute] Guid projectId,
        [FromRoute] Guid keyId,
        CancellationToken ct)
    {
        _ = projectId;
        var command = new RevokeApiKeyCommand(keyId);
        await revokeApiKeyUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }
}

