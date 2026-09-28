using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;

public record UpdateDataConnectionCommand(
    Guid Id,
    string Name,
    string Provider,
    string ConnectionString);

public sealed class UpdateDataConnectionUseCase(
    IDataConnectionRepository repository,
    IUnitOfWork unitOfWork,
    IDataProtectionService dataProtection) : IUseCase<UpdateDataConnectionCommand, DataConnectionDto?>
{
    private readonly IDataConnectionRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDataProtectionService _dataProtection = dataProtection;

    public async Task<DataConnectionDto?> ExecuteAsync(UpdateDataConnectionCommand command, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(command.Id, ct);
        if (entity == null) return null;

        var encryptedCs = string.IsNullOrEmpty(command.ConnectionString)
            ? entity.EncryptedConnectionString
            : _dataProtection.Encrypt(command.ConnectionString);

        entity.UpdateConnection(command.Name, command.Provider, encryptedCs);

        _repository.Update(entity);
        await _unitOfWork.CommitAsync(ct);

        return new DataConnectionDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Provider = entity.Provider,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
