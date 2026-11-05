using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Queries.GetDataConnectionById;

public sealed class GetDataConnectionByIdUseCase(
    IDataConnectionRepository repository) : IUseCase<GetDataConnectionByIdQuery, DataConnectionResultDto>
{
    public async Task<DataConnectionResultDto> ExecuteAsync(GetDataConnectionByIdQuery query, CancellationToken ct = default)
    {
        var c = await repository.GetByIdAsync(new DataConnectionId(query.Id), ct)
            ?? throw new NotFoundException("DataConnection", query.Id);

        return new DataConnectionResultDto(
            c.Id.Value,
            c.Name.Value,
            c.Provider.Name,
            c.CreatedAt,
            c.UpdatedAt,
            (c.UpdatedAt ?? c.CreatedAt).ToUnixTimeMilliseconds().ToString());
    }
}
