using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;

public record CreateDataConnectionCommand(
    string Name,
    string Provider,
    string ConnectionString);

public sealed class CreateDataConnectionUseCase(
    IDataConnectionRepository repository,
    IUnitOfWork unitOfWork,
    IDataProtectionService dataProtection) : IUseCase<CreateDataConnectionCommand, DataConnectionDto>
{
    private readonly IDataConnectionRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDataProtectionService _dataProtection = dataProtection;

    public async Task<DataConnectionDto> ExecuteAsync(CreateDataConnectionCommand command, CancellationToken ct = default)
    {
        var entity = new DataConnection(
            command.Name,
            command.Provider,
            _dataProtection.Encrypt(command.ConnectionString));

        await _repository.AddAsync(entity, ct);
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
