using FluentValidation;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Validators;

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
            .Must(BeAValidDatabaseProvider)
            .WithMessage("Database provider must be one of: PostgreSQL, SqlServer, MySQL, Oracle.");

        RuleFor(x => x.ConnectionString)
            .NotEmpty().WithMessage("Connection string is required.")
            .MaximumLength(4000).WithMessage("Connection string cannot exceed 4000 characters.");
    }

    private static bool BeAValidDatabaseProvider(string provider)
    {
        return !string.IsNullOrWhiteSpace(provider) &&
               Enumeration.GetAll<DatabaseProvider>().Any(p => p.Name.Equals(provider, StringComparison.OrdinalIgnoreCase));
    }
}

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
            .Must(BeAValidDatabaseProvider)
            .WithMessage("Database provider must be one of: PostgreSQL, SqlServer, MySQL, Oracle.");

        RuleFor(x => x.ConnectionString)
            .MaximumLength(4000).WithMessage("Connection string cannot exceed 4000 characters.");
    }

    private static bool BeAValidDatabaseProvider(string provider)
    {
        return !string.IsNullOrWhiteSpace(provider) &&
               Enumeration.GetAll<DatabaseProvider>().Any(p => p.Name.Equals(provider, StringComparison.OrdinalIgnoreCase));
    }
}
