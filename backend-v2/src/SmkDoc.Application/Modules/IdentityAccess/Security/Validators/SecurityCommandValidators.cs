using FluentValidation;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Validators;

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
