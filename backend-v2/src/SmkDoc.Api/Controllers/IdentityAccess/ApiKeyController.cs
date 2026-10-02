using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

namespace SmkDoc.Api.Controllers;

[ApiController]
[Route("api/api-keys")]
public class ApiKeyController(
    ListApiKeysUseCase listApiKeysUseCase,
    CreateApiKeyUseCase createApiKeyUseCase,
    RevokeApiKeyUseCase revokeApiKeyUseCase) : ControllerBase
{
    private readonly ListApiKeysUseCase _listApiKeysUseCase = listApiKeysUseCase;
    private readonly CreateApiKeyUseCase _createApiKeyUseCase = createApiKeyUseCase;
    private readonly RevokeApiKeyUseCase _revokeApiKeyUseCase = revokeApiKeyUseCase;

    [HttpGet]
    public async Task<IActionResult> ListKeys(CancellationToken ct)
    {
        var keys = await _listApiKeysUseCase.ExecuteAsync(new ListApiKeysQuery(), ct);
        return Ok(new { keys });
    }

    public record CreateApiKeyRequest(string Name, string CallerApp, Guid? ProjectId = null);

    [HttpPost]
    public async Task<IActionResult> CreateKey([FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        var result = await _createApiKeyUseCase.ExecuteAsync(new CreateApiKeyCommand(request.Name, request.CallerApp, request.ProjectId), ct);
        return Ok(new
        {
            id = result.Id,
            name = result.Name,
            callerApp = result.CallerApp,
            key = result.PlainTextKey // shown once
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RevokeKey([FromRoute] Guid id, CancellationToken ct)
    {
        await _revokeApiKeyUseCase.ExecuteAsync(new RevokeApiKeyCommand(id), ct);
        return Ok(new { success = true });
    }
}
