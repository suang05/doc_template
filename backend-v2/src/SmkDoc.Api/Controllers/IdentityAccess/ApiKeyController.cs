using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Contracts.IdentityAccess.ApiKeys;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

namespace SmkDoc.Api.Controllers.IdentityAccess;

/// <summary>
/// Flat API Key management endpoints for legacy Portal UI compatibility.
/// Canonical multi-tenant endpoints are in <see cref="ApiKeyManagementController"/>.
/// Auth: Channel B (Bearer JWT).
/// </summary>
[ApiController]
[Route("api/api-keys")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class ApiKeyController(
    ListApiKeysUseCase listApiKeysUseCase,
    CreateApiKeyUseCase createApiKeyUseCase,
    RevokeApiKeyUseCase revokeApiKeyUseCase,
    IExecutionContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListKeys(
        [FromQuery] Guid? projectId,
        CancellationToken ct)
    {
        if (projectId.HasValue && context.ProjectId.HasValue && projectId.Value != context.ProjectId.Value)
        {
            throw new BadHttpRequestException("Access denied: requested projectId does not match the active project context.");
        }

        var targetProjectId = context.ProjectId ?? projectId
            ?? throw new BadHttpRequestException("ProjectId is required to list API keys.");
        var query = new ListApiKeysQuery(targetProjectId);
        var keys = await listApiKeysUseCase.ExecuteAsync(query, ct);
        return Ok(new { keys });
    }

    [HttpPost]
    public async Task<IActionResult> CreateKey([FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        if (context.ProjectId.HasValue && request.ProjectId != Guid.Empty && request.ProjectId != context.ProjectId.Value)
        {
            throw new BadHttpRequestException("Access denied: requested projectId does not match the active project context.");
        }

        var targetProjectId = context.ProjectId ?? request.ProjectId;
        var command = new CreateApiKeyCommand(request.Name, request.CallerApp, targetProjectId);
        var result = await createApiKeyUseCase.ExecuteAsync(command, ct);
        return Ok(new
        {
            id = result.Id,
            name = result.Name,
            callerApp = result.CallerApp,
            key = result.PlainTextKey // shown once
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RevokeKey(
        [FromRoute] Guid id,
        [FromQuery] Guid? projectId,
        CancellationToken ct)
    {
        if (projectId.HasValue && context.ProjectId.HasValue && projectId.Value != context.ProjectId.Value)
        {
            throw new BadHttpRequestException("Access denied: requested projectId does not match the active project context.");
        }

        var targetProjectId = context.ProjectId ?? projectId
            ?? throw new BadHttpRequestException("ProjectId is required to revoke an API key.");
        var command = new RevokeApiKeyCommand(id, targetProjectId);
        await revokeApiKeyUseCase.ExecuteAsync(command, ct);
        return Ok(new { success = true });
    }
}
