using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets;

namespace SmkDoc.Api.Controllers;

[ApiController]
[Route("api/datasets")]
public class DatasetController : ControllerBase
{
    private readonly DatasetUseCase _useCase;

    public DatasetController(DatasetUseCase useCase) => _useCase = useCase;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _useCase.GetAllAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _useCase.GetByIdAsync(id, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDatasetDto dto, CancellationToken ct)
    {
        var result = await _useCase.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDatasetDto dto, CancellationToken ct)
    {
        var result = await _useCase.UpdateAsync(id, dto, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var success = await _useCase.DeleteAsync(id, ct);
        if (!success) return NotFound();
        return NoContent();
    }
}
