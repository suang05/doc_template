using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;

public record DeleteDatasetCommand(Guid Id);

public sealed class DeleteDatasetUseCase(
    IDatasetRepository datasetRepo,
    IUnitOfWork unitOfWork) : IUseCase<DeleteDatasetCommand, bool>
{
    public async Task<bool> ExecuteAsync(DeleteDatasetCommand command, CancellationToken ct = default)
    {
        var entity = await datasetRepo.GetByIdAsync(new DatasetId(command.Id), ct);
        if (entity == null) return false;

        datasetRepo.Remove(entity);
        await unitOfWork.CommitAsync(ct);
        return true;
    }
}
