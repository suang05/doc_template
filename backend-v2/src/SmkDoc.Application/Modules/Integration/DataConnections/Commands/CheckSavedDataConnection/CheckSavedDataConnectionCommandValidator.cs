using FluentValidation;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.CheckSavedDataConnection;

public sealed class CheckSavedDataConnectionCommandValidator : AbstractValidator<CheckSavedDataConnectionCommand>
{
    public CheckSavedDataConnectionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
