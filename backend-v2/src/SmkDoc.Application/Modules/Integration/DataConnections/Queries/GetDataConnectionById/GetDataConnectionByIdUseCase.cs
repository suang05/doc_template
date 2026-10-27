using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Queries.GetDataConnectionById;

public record GetDataConnectionByIdQuery(Guid Id);

public sealed class GetDataConnectionByIdUseCase(
    IDataConnectionRepository repository) : IUseCase<GetDataConnectionByIdQuery, DataConnectionDto?>
{
    public async Task<DataConnectionDto?> ExecuteAsync(GetDataConnectionByIdQuery query, CancellationToken ct = default)
    {
        var c = await repository.GetByIdAsync(new DataConnectionId(query.Id), ct);
        if (c == null) return null;

        return new DataConnectionDto(
            c.Id.Value,
            c.Name.Value,
            c.Provider.Name,
            c.CreatedAt,
            c.UpdatedAt);
    }
}
