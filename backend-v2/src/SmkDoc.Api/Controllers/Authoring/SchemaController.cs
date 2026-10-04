using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Application.Modules.Authoring.Schemas.DTOs;
using SmkDoc.Application.Modules.Authoring.Schemas;
using System.Text.Json;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Standalone JSON Schema Draft-07 validation endpoints.
/// Zero side-effects and zero database dependencies.
/// </summary>
[ApiController]
[Route("api/v1/schemas")]
[Route("api/schemas")]
public class SchemaController : ControllerBase
{
    private readonly ValidateStandaloneSchemaUseCase _validateUseCase;

    public SchemaController(ValidateStandaloneSchemaUseCase validateUseCase)
    {
        _validateUseCase = validateUseCase;
    }

    /// <summary>
    /// Standalone validation of a JSON payload against a Draft-07 JSON Schema.
    /// Returns 200 OK with validation result details (valid = true/false and error items).
    /// </summary>
    /// <param name="command">Request containing both schema and payload JsonElements.</param>
    /// <returns>ApiResponse containing <see cref="ValidateStandaloneSchemaResult"/>.</returns>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ApiResponse<ValidateStandaloneSchemaResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public IActionResult Validate([FromBody] ValidateStandaloneSchemaCommand command)
    {
        if (command == null ||
            command.Schema.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ||
            command.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return BadRequest(new ApiResponse<object>(new
            {
                code = "INVALID_REQUEST",
                message = "Both 'schema' and 'payload' must be provided in the request body."
            }));
        }

        var result = _validateUseCase.Execute(command);
        return Ok(new ApiResponse<ValidateStandaloneSchemaResult>(result));
    }
}
