using FluentValidation;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Integration.Datasets.Validators;

public sealed class CreateDatasetCommandValidator : AbstractValidator<CreateDatasetCommand>
{
    public CreateDatasetCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Dataset name cannot be empty.")
            .MaximumLength(DatasetName.MaxLength)
            .WithMessage($"Dataset name cannot exceed {DatasetName.MaxLength} characters.");

        RuleFor(x => x.DataConnectionId)
            .NotEmpty().WithMessage("DataConnectionId is required.");

        RuleFor(x => x.SqlQuery)
            .NotEmpty().WithMessage("SQL query cannot be empty.")
            .MaximumLength(SqlQuery.MaxQueryLength)
            .WithMessage($"SQL query cannot exceed {SqlQuery.MaxQueryLength} characters.");

        RuleFor(x => x.CacheSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Cache seconds must be greater than or equal to 0.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}

public sealed class UpdateDatasetCommandValidator : AbstractValidator<UpdateDatasetCommand>
{
    public UpdateDatasetCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Dataset ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Dataset name cannot be empty.")
            .MaximumLength(DatasetName.MaxLength)
            .WithMessage($"Dataset name cannot exceed {DatasetName.MaxLength} characters.");

        RuleFor(x => x.DataConnectionId)
            .NotEmpty().WithMessage("DataConnectionId is required.");

        RuleFor(x => x.SqlQuery)
            .NotEmpty().WithMessage("SQL query cannot be empty.")
            .MaximumLength(SqlQuery.MaxQueryLength)
            .WithMessage($"SQL query cannot exceed {SqlQuery.MaxQueryLength} characters.");

        RuleFor(x => x.CacheSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Cache seconds must be greater than or equal to 0.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
