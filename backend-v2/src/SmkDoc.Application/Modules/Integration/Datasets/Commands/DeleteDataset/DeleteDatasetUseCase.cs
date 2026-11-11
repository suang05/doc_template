using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Extensions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;

public sealed class DeleteDatasetUseCase(
    IDatasetRepository datasetRepo,
    IUnitOfWork unitOfWork) : IUseCase<DeleteDatasetCommand>
{
    public async Task ExecuteAsync(DeleteDatasetCommand command, CancellationToken ct = default)
    {
        var entity = await datasetRepo.GetByIdAsync(new DatasetId(command.Id), ct)
            ?? throw new NotFoundException("Dataset", command.Id);

        datasetRepo.Remove(entity);
        await unitOfWork.CommitAsync(ct);
    }
}
