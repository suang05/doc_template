using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;

public record DeleteDataConnectionCommand(Guid Id);

public sealed class DeleteDataConnectionUseCase(
    IDataConnectionRepository repository,
    IUnitOfWork unitOfWork) : IUseCase<DeleteDataConnectionCommand, bool>
{
    public async Task<bool> ExecuteAsync(DeleteDataConnectionCommand command, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(new DataConnectionId(command.Id), ct);
        if (entity == null) return false;

        repository.Remove(entity);
        await unitOfWork.CommitAsync(ct);
        return true;
    }
}
