using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Extensions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;

public sealed class DeleteDataConnectionUseCase(
    IDataConnectionRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext) : IUseCase<DeleteDataConnectionCommand>
{
    public async Task ExecuteAsync(DeleteDataConnectionCommand command, CancellationToken ct = default)
    {
        projectContext.EnsureResolved();

        var entity = await repository.GetByIdAsync(new DataConnectionId(command.Id), ct)
            ?? throw new NotFoundException("DataConnection", command.Id);

        repository.Remove(entity);
        await unitOfWork.CommitAsync(ct);
    }
}
