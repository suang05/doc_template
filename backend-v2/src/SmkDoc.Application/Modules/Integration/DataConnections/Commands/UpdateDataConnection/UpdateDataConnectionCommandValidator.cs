using FluentValidation;
using SmkDoc.Application.Modules.Integration.DataConnections.Helpers;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;

public sealed class UpdateDataConnectionCommandValidator : AbstractValidator<UpdateDataConnectionCommand>
{
    public UpdateDataConnectionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Connection ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Connection name cannot be empty.")
            .MaximumLength(ConnectionName.MaxLength)
            .WithMessage($"Connection name cannot exceed {ConnectionName.MaxLength} characters.");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Database provider is required.")
            .Must(DataConnectionValidatorRules.BeAValidDatabaseProvider)
            .WithMessage("Database provider must be one of: PostgreSQL, SqlServer, MySQL, Oracle.");

        RuleFor(x => x.ConnectionString)
            .MaximumLength(4000).WithMessage("Connection string cannot exceed 4000 characters.");
    }
}
