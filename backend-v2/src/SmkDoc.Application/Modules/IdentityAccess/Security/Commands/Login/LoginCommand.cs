namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;

/// <summary>
/// Command for authenticating a user with email and password.
/// </summary>
public record LoginCommand
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    public LoginCommand() { }

    public LoginCommand(string email, string password)
    {
        Email = email;
        Password = password;
    }
}
