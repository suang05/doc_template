using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.DTOs.DataConnections;
using SmkDoc.Application.UseCases.DataConnections;

namespace SmkDoc.Api.Controllers;

[ApiController]
[Route("api/data-connections")]
public class DataConnectionsController : ControllerBase
{
    private readonly DataConnectionUseCase _useCase;

    public DataConnectionsController(DataConnectionUseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _useCase.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _useCase.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDataConnectionDto dto)
    {
        var result = await _useCase.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDataConnectionDto dto)
    {
        var result = await _useCase.UpdateAsync(id, dto);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _useCase.DeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("test")]
    public async Task<IActionResult> TestConnection([FromBody] TestDataConnectionDto dto)
    {
        var success = await _useCase.TestConnectionAsync(dto);
        if (success) return Ok(new { message = "Connection successful." });
        return BadRequest(new { error = "Connection failed." });
    }
}
