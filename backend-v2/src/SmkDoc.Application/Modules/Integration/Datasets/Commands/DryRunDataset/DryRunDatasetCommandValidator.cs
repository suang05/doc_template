using FluentValidation;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.DryRunDataset;

public sealed class DryRunDatasetCommandValidator : AbstractValidator<DryRunDatasetCommand>
{
    public DryRunDatasetCommandValidator()
    {
        RuleFor(x => x.SqlQuery)
            .NotEmpty().WithMessage("SQL query cannot be empty.")
            .MaximumLength(SqlQuery.MaxQueryLength)
            .WithMessage($"SQL query cannot exceed {SqlQuery.MaxQueryLength} characters.");

        RuleFor(x => x.MaxSampleRows)
            .InclusiveBetween(1, 50).WithMessage("MaxSampleRows must be between 1 and 50.");

        RuleFor(x => x)
            .Must(cmd => (cmd.DataConnectionId.HasValue && cmd.DataConnectionId.Value != Guid.Empty) ||
                         (!string.IsNullOrWhiteSpace(cmd.Provider) && !string.IsNullOrWhiteSpace(cmd.ConnectionString)))
            .WithMessage("Either DataConnectionId or both Provider and ConnectionString must be specified.");
    }
}

