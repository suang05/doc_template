using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmkDocServer.Domain.Interfaces;

namespace SmkDocServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataConnectionController : ControllerBase
{
    private readonly IDataConnectionService _service;
    private readonly ILogger<DataConnectionController> _logger;

    public DataConnectionController(IDataConnectionService service, ILogger<DataConnectionController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>List all DataConnections, optionally filtered by project.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? projectId = null)
    {
        var list = await _service.GetAllAsync(projectId);
        return Ok(list);
    }

    /// <summary>Create a new DataConnection (connection string encrypted at rest).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DataConnectionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Name is required." });
        if (string.IsNullOrWhiteSpace(dto.ConnectionString))
            return BadRequest(new { message = "ConnectionString is required." });

        var created = await _service.CreateAsync(dto);
        return Ok(created);
    }

    /// <summary>Update a DataConnection (connection string optional — only updated if provided).</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DataConnectionDto dto)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"DataConnection {id} not found." });
        }
    }

    /// <summary>Delete a DataConnection.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return Ok(new { message = "Deleted." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"DataConnection {id} not found." });
        }
    }

    /// <summary>Test connectivity for a DataConnection.</summary>
    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> Test(Guid id)
    {
        bool ok = await _service.TestAsync(id);
        return Ok(new { success = ok, message = ok ? "Connection successful." : "Connection failed." });
    }
}
