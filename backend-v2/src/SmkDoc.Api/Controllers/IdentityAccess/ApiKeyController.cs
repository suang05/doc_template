using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.Modules.IdentityAccess.Security;

namespace SmkDoc.Api.Controllers;

[ApiController]
[Route("api/api-keys")]
public class ApiKeyController : ControllerBase
{
    private readonly ApiKeyUseCase _apiKeyUseCase;

    public ApiKeyController(ApiKeyUseCase apiKeyUseCase)
    {
        _apiKeyUseCase = apiKeyUseCase;
    }

    [HttpGet]
    public async Task<IActionResult> ListKeys(CancellationToken ct)
    {
        var keys = await _apiKeyUseCase.ListKeysAsync(ct);
        return Ok(new { keys });
    }

    public record CreateApiKeyRequest(string Name, string CallerApp, Guid? ProjectId = null);

    [HttpPost]
    public async Task<IActionResult> CreateKey([FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        var result = await _apiKeyUseCase.CreateKeyAsync(request.Name, request.CallerApp, request.ProjectId, ct);
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
        await _apiKeyUseCase.RevokeKeyAsync(id, ct);
        return Ok(new { success = true });
    }
}
