using FluentValidation;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;

public sealed class DeleteDataConnectionCommandValidator : AbstractValidator<DeleteDataConnectionCommand>
{
    public DeleteDataConnectionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Connection ID is required.");
    }
}
