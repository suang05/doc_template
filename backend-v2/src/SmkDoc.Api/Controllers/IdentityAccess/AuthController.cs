using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Contracts.IdentityAccess.Auth;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using LoginRequest = SmkDoc.Api.Contracts.IdentityAccess.Auth.LoginRequest;

namespace SmkDoc.Api.Controllers.IdentityAccess;

[ApiController]
[Route("api/auth")]
public class AuthController(LoginUseCase loginUseCase) : ControllerBase
{
    private readonly LoginUseCase _loginUseCase = loginUseCase;

    /// <summary>
    /// Authenticate a user and return a JWT access token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var command = new LoginCommand(request.Email, request.Password, request.ProjectId);
        var response = await _loginUseCase.ExecuteAsync(command, ct);
        return Ok(response);
    }
}
