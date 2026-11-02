using System.Text.Json;
using FluentValidation;

namespace SmkDoc.Application.Modules.Authoring.Schemas.Commands.ValidateStandaloneSchema;

public sealed class ValidateStandaloneSchemaCommandValidator : AbstractValidator<ValidateStandaloneSchemaCommand>
{
    public ValidateStandaloneSchemaCommandValidator()
    {
        RuleFor(x => x.Schema)
            .Must(s => s.ValueKind is JsonValueKind.Object or JsonValueKind.True or JsonValueKind.False)
            .WithMessage("Schema must be a valid JSON object or boolean schema.");

        RuleFor(x => x.Payload)
            .Must(p => p.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
            .WithMessage("Payload data is required.");
    }
}
