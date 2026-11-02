using FluentValidation;

namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;

public sealed class DeleteDatasetCommandValidator : AbstractValidator<DeleteDatasetCommand>
{
    public DeleteDatasetCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Dataset ID is required.");
    }
}
