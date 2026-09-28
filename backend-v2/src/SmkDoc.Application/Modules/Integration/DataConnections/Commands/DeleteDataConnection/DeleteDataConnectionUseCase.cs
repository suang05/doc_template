using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;

public record DeleteDataConnectionCommand(Guid Id);

public sealed class DeleteDataConnectionUseCase(
    IDataConnectionRepository repository,
    IUnitOfWork unitOfWork) : IUseCase<DeleteDataConnectionCommand, bool>
{
    private readonly IDataConnectionRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<bool> ExecuteAsync(DeleteDataConnectionCommand command, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(command.Id, ct);
        if (entity == null) return false;

        _repository.Remove(entity);
        await _unitOfWork.CommitAsync(ct);
        return true;
    }
}
