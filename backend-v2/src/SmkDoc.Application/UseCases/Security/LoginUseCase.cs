using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.UseCases.Security;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; } = 86400; // 24 hours
}

public class LoginUseCase
{
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<UserProjectRole> _roleRepo;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginUseCase(
        IRepository<User> userRepo, 
        IRepository<UserProjectRole> roleRepo,
        IPasswordHasher passwordHasher, 
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepo = userRepo;
        _roleRepo = roleRepo;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<LoginResponse> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userRepo.FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, ct);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var role = await _roleRepo.FirstOrDefaultAsync(r => r.UserId == user.Id && r.ProjectId == request.ProjectId, ct);
        if (role == null)
        {
            throw new UnauthorizedAccessException("User does not have access to the specified project.");
        }

        var roles = new List<string> { role.Role.ToString() };
        var token = _jwtTokenGenerator.GenerateToken(user, request.ProjectId, roles);

        return new LoginResponse
        {
            AccessToken = token
        };
    }
}
