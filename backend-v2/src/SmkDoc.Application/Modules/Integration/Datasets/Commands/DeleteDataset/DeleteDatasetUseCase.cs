using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;

public sealed class DeleteDatasetUseCase(
    IDatasetRepository datasetRepo,
    IUnitOfWork unitOfWork) : IUseCase<DeleteDatasetCommand, bool>
{
    public async Task<bool> ExecuteAsync(DeleteDatasetCommand command, CancellationToken ct = default)
    {
        var entity = await datasetRepo.GetByIdAsync(new DatasetId(command.Id), ct)
            ?? throw new NotFoundException("Dataset", command.Id);

        if (!string.IsNullOrWhiteSpace(command.IfMatch) && command.IfMatch != "*")
        {
            var currentToken = (entity.UpdatedAt ?? entity.CreatedAt).ToUnixTimeMilliseconds().ToString();
            var sanitizedMatch = command.IfMatch.Trim('\"', 'W', '/', ' ');
            if (!string.Equals(sanitizedMatch, currentToken, StringComparison.OrdinalIgnoreCase))
            {
                throw new PreconditionFailedException(
                    $"Precondition failed for dataset '{command.Id}'. ETag '{sanitizedMatch}' does not match current state.");
            }
        }

        datasetRepo.Remove(entity);
        await unitOfWork.CommitAsync(ct);
        return true;
    }
}
