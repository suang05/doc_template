using FluentValidation;
using SmkDoc.Application.DTOs.Security;
using SmkDoc.Application.DTOs.Users;

namespace SmkDoc.Application.Validators.Security;

/// <summary>
/// Validator for <see cref="LoginCommand"/>.
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");
    }
}

/// <summary>
/// Validator for <see cref="CreateApiKeyCommand"/>.
/// </summary>
public sealed class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("API Key name is required.")
            .MaximumLength(100).WithMessage("API Key name cannot exceed 100 characters.");

        RuleFor(x => x.CallerApp)
            .NotEmpty().WithMessage("Caller app identifier is required.")
            .MaximumLength(100).WithMessage("Caller app identifier cannot exceed 100 characters.");
    }
}

/// <summary>
/// Validator for <see cref="InviteUserCommand"/>.
/// </summary>
public sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin", "Editor", "Viewer"
    };

    public InviteUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(50).WithMessage("First name cannot exceed 50 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(50).WithMessage("Last name cannot exceed 50 characters.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(x => AllowedRoles.Contains(x))
            .WithMessage("Role must be one of: 'Admin', 'Editor', 'Viewer'.");
    }
}
