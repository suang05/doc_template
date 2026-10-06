using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;

public record CreateDataConnectionCommand(
    string Name,
    string Provider,
    string ConnectionString);

public sealed class CreateDataConnectionUseCase(
    IDataConnectionRepository repository,
    IUnitOfWork unitOfWork,
    IDataProtectionService dataProtection,
    TimeProvider? timeProvider = null) : IUseCase<CreateDataConnectionCommand, DataConnectionDto>
{
    private readonly IDataConnectionRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDataProtectionService _dataProtection = dataProtection;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DataConnectionDto> ExecuteAsync(CreateDataConnectionCommand command, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        var entity = DataConnection.Create(
            ConnectionName.Create(command.Name),
            Enumeration.FromDisplayName<DatabaseProvider>(command.Provider),
            _dataProtection.Encrypt(command.ConnectionString),
            now);

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.CommitAsync(ct);

        return new DataConnectionDto
        {
            Id = entity.Id,
            Name = entity.Name.Value,
            Provider = entity.Provider.Name,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
