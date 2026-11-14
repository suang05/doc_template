using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;

public sealed class DeleteDataConnectionUseCase(
    IDataConnectionRepository repository,
    IUnitOfWork unitOfWork) : IUseCase<DeleteDataConnectionCommand>
{
    public async Task ExecuteAsync(DeleteDataConnectionCommand command, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(new DataConnectionId(command.Id), ct)
            ?? throw new NotFoundException("DataConnection", command.Id);

        repository.Remove(entity);
        await unitOfWork.CommitAsync(ct);
    }
}
