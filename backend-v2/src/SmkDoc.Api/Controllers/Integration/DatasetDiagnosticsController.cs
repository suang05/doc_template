using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Integration.Datasets;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.DryRunDataset;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;

namespace SmkDoc.Api.Controllers.Integration;

/// <summary>
/// SQL dataset diagnostics and schema introspection.
/// Auth: Author, Developer, Admin, SuperAdmin via AuthorizationPolicies.CanManageDatasets.
/// </summary>
[ApiController]
[Route("api/v1/datasets")]
[Authorize(Policy = AuthorizationPolicies.CanManageDatasets)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class DatasetDiagnosticsController(
    IUseCase<DryRunDatasetCommand, DryRunDatasetResultDto> dryRunUseCase) : ControllerBase
{
    /// <summary>Dry-run a SQL dataset query to discover column schema and preview sample rows.</summary>
    [HttpPost("dry-run")]
    [EnableRateLimiting("test-conn")]
    [ProducesResponseType(typeof(ApiResponse<DryRunDatasetResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DryRun([FromBody] DryRunDatasetRequest request, CancellationToken ct)
    {
        var command = new DryRunDatasetCommand(
            request.DataConnectionId,
            request.Provider,
            request.ConnectionString,
            request.SqlQuery,
            request.Parameters,
            request.MaxSampleRows);
        var result = await dryRunUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<DryRunDatasetResultDto>(result));
    }
}

