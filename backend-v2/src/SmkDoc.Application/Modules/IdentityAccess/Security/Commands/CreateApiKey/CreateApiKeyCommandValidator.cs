using FluentValidation;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;

public sealed class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("API Key name is required.")
            .MaximumLength(100).WithMessage("API Key name must not exceed 100 characters.");

        RuleFor(x => x.CallerApp)
            .NotEmpty().WithMessage("CallerApp is required.")
            .MaximumLength(50).WithMessage("CallerApp must not exceed 50 characters.");

        RuleFor(x => x.Scope)
            .NotEmpty().WithMessage("Scope is required.")
            .Must(scope => ApiKeyScope.TryFromName(scope, out _))
            .WithMessage("Invalid Scope. Allowed scopes are: ReadOnly, ReadWrite.");
    }
}
