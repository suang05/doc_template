using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;

public record DeleteDatasetCommand(Guid Id);

public sealed class DeleteDatasetUseCase(
    IDatasetRepository datasetRepo,
    IUnitOfWork unitOfWork) : IUseCase<DeleteDatasetCommand, bool>
{
    private readonly IDatasetRepository _datasetRepo = datasetRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<bool> ExecuteAsync(DeleteDatasetCommand command, CancellationToken ct = default)
    {
        var entity = await _datasetRepo.GetByIdAsync(command.Id, ct);
        if (entity == null) return false;

        _datasetRepo.Remove(entity);
        await _unitOfWork.CommitAsync(ct);
        return true;
    }
}
