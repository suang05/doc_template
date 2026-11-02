using FluentValidation;
using SmkDoc.Application.Modules.Integration.DataConnections.Helpers;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;

public sealed class TestDataConnectionCommandValidator : AbstractValidator<TestDataConnectionCommand>
{
    public TestDataConnectionCommandValidator()
    {
        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Database provider is required.")
            .Must(DataConnectionValidatorRules.BeAValidDatabaseProvider)
            .WithMessage("Database provider must be one of: PostgreSQL, SqlServer, MySQL, Oracle.");

        RuleFor(x => x.ConnectionString)
            .NotEmpty().WithMessage("Connection string is required.")
            .MaximumLength(4000).WithMessage("Connection string cannot exceed 4000 characters.");
    }
}
