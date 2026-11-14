using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;

public sealed class DeleteDatasetUseCase(
    IDatasetRepository datasetRepo,
    IUnitOfWork unitOfWork) : IUseCase<DeleteDatasetCommand>
{
    public async Task ExecuteAsync(DeleteDatasetCommand command, CancellationToken ct)
    {
        var entity = await datasetRepo.GetByIdAsync(new DatasetId(command.Id), ct)
            ?? throw new NotFoundException("Dataset", command.Id);

        datasetRepo.Remove(entity);
        await unitOfWork.CommitAsync(ct);
    }
}
