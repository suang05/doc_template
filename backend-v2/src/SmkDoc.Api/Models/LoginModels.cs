using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

namespace SmkDoc.Api.Models;

public record LoginRequest(
    string Email,
    string Password,
    Guid? ProjectId = null
) : LoginCommand(Email, Password, ProjectId);
