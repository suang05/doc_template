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
public sealed class ApiKeyController(
    IUseCase<ListApiKeysQuery, List<ApiKeyDto>> listApiKeysUseCase,
    IUseCase<CreateApiKeyCommand, CreateApiKeyResultDto> createApiKeyUseCase,
    IUseCase<RevokeApiKeyCommand> revokeApiKeyUseCase) : ControllerBase
{
    /// <summary>List all active and inactive sub-keys for the current project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ApiKeyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListKeys(CancellationToken ct)
    {
        var query = new ListApiKeysQuery();
        var result = await listApiKeysUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<ApiKeyDto>>(result));
    }

    /// <summary>Generate a new sub-key for the current project under quota guardrails (Max 2 active keys).</summary>
    [HttpPost]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<CreateApiKeyResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateKey([FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        var scope = !string.IsNullOrWhiteSpace(request.Scope)
            ? request.Scope
            : (request.Scopes?.FirstOrDefault() ?? "ReadWrite");

        var command = new CreateApiKeyCommand(request.Name, request.CallerApp, scope);
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
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var command = new RevokeApiKeyCommand(id);
        await revokeApiKeyUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }
}
