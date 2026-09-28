using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.UseCases.Templates.Commands.DeactivateTemplate;

/// <summary>
/// Single-responsibility Use Case for deactivating a template.
/// </summary>
public sealed class DeactivateTemplateUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork) : IUseCase<DeactivateTemplateCommand>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task ExecuteAsync(DeactivateTemplateCommand command, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(command.TemplateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' was not found.");

        template.Deactivate();

        _templateRepo.Update(template);
        await _unitOfWork.CommitAsync(ct);
    }
}
