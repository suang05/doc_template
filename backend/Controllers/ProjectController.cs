using Microsoft.AspNetCore.Mvc;
using SmkDocServer.Domain.Entities;
using SmkDocServer.Domain.Interfaces;

namespace SmkDocServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(IApiKeyService apiKeyService, ILogger<ProjectController> logger)
    {
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var projects = await _apiKeyService.GetAllProjectsAsync();
        return Ok(projects);
    }

    public record CreateProjectDto(string Code, string Name, string? Description);

    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Code and Name are required." });

        try
        {
            var project = await _apiKeyService.CreateProjectAsync(dto.Code, dto.Name, dto.Description);
            return Ok(project);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    public record CreateApiKeyDto(string Name, DateTime? ExpiresAt);

    [HttpPost("{projectId:guid}/keys")]
    public async Task<IActionResult> CreateApiKey(Guid projectId, [FromBody] CreateApiKeyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Key name is required." });

        try
        {
            var apiKey = await _apiKeyService.CreateApiKeyAsync(projectId, dto.Name, dto.ExpiresAt);
            return Ok(apiKey);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("keys/{keyId:guid}")]
    public async Task<IActionResult> RevokeApiKey(Guid keyId)
    {
        bool revoked = await _apiKeyService.RevokeApiKeyAsync(keyId);
        if (!revoked)
            return NotFound(new { error = "API Key not found" });

        return Ok(new { message = "API Key status toggled successfully." });
    }
}
