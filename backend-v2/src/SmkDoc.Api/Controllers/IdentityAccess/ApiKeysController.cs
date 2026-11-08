using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
/// Tenant Sub-Key lifecycle management for Project Admins and Owners under ambient guardrails.
/// Auth: JWT Bearer — ProjectAdmin role required. Operates strictly on ambient project context.
/// </summary>
[ApiController]
[Route("api/v1/api-keys")]
[Authorize(Policy = AuthorizationPolicies.RequireProjectAdmin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class ApiKeysController(
    IUseCase<ListApiKeysQuery, List<ApiKeyResultDto>> listApiKeysUseCase,
    IUseCase<CreateApiKeyCommand, CreateApiKeyResultDto> createApiKeyUseCase,
    IUseCase<RevokeApiKeyCommand> revokeApiKeyUseCase) : ControllerBase
{
    /// <summary>List all active and inactive sub-keys for the current project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ApiKeyResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListKeys(CancellationToken ct)
    {
        var query = new ListApiKeysQuery();
        var result = await listApiKeysUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<ApiKeyResultDto>>(result));
    }

    /// <summary>Generate a new sub-key for the current project under quota guardrails (Max 2 active keys).</summary>
    [HttpPost]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<CreateApiKeyResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateKey([FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        var command = new CreateApiKeyCommand(request.Name, request.CallerApp, request.EffectiveScope);
        var result = await createApiKeyUseCase.ExecuteAsync(command, ct);
        return CreatedAtAction(nameof(ListKeys), new ApiResponse<CreateApiKeyResultDto>(result));
    }

    /// <summary>Revoke a sub-key belonging to the current project (Self-service key rotation).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> RevokeKey(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var command = new RevokeApiKeyCommand(id);
        await revokeApiKeyUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }
}

