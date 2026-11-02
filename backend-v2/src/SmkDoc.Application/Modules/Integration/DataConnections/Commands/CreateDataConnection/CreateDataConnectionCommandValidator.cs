using FluentValidation;
using SmkDoc.Application.Modules.Integration.DataConnections.Helpers;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;

public sealed class CreateDataConnectionCommandValidator : AbstractValidator<CreateDataConnectionCommand>
{
    public CreateDataConnectionCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Connection name cannot be empty.")
            .MaximumLength(ConnectionName.MaxLength)
            .WithMessage($"Connection name cannot exceed {ConnectionName.MaxLength} characters.");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Database provider is required.")
            .Must(DataConnectionValidatorRules.BeAValidDatabaseProvider)
            .WithMessage("Database provider must be one of: PostgreSQL, SqlServer, MySQL, Oracle.");

        RuleFor(x => x.ConnectionString)
            .NotEmpty().WithMessage("Connection string is required.")
            .MaximumLength(4000).WithMessage("Connection string cannot exceed 4000 characters.");
    }
}
