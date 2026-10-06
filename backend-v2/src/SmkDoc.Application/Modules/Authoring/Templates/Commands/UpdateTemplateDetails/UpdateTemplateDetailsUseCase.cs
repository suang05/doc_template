using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;

/// <summary>
/// Single-responsibility Use Case for updating template metadata (name, category).
/// Enforces domain encapsulation through entity method calls.
/// </summary>
public sealed class UpdateTemplateDetailsUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork,
    IValidator<UpdateTemplateDetailsCommand> validator,
    TimeProvider? timeProvider = null) : IUseCase<UpdateTemplateDetailsCommand, TemplateResultDto>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IValidator<UpdateTemplateDetailsCommand> _validator = validator;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<TemplateResultDto> ExecuteAsync(UpdateTemplateDetailsCommand command, CancellationToken ct = default)
    {
        // 1. Fail-Fast Validation
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        // 2. Fetch Aggregate Root
        var template = await _templateRepo.GetByIdAsync(command.TemplateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' was not found.");

        // 3. Domain Invariants Execution
        var now = _timeProvider.GetUtcNow();
        template.UpdateDetails(TemplateName.Create(command.Name), command.Category?.Trim(), now);

        // 4. Persistence & Atomic Commit
        _templateRepo.Update(template);
        await _unitOfWork.CommitAsync(ct);

        // 5. Return Safe DTO
        return new TemplateResultDto(
            template.Id,
            template.ProjectId,
            template.Name,
            template.Slug,
            template.Category,
            template.IsActive,
            template.CurrentVersionId,
            template.CurrentVersion?.FileFormat?.Name,
            template.CreatedAt,
            template.UpdatedAt
        );
    }
}
