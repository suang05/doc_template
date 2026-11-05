using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;

public sealed class ListDataConnectionsUseCase(
    IDataConnectionRepository repository) : IUseCase<ListDataConnectionsQuery, List<DataConnectionResultDto>>
{
    public async Task<List<DataConnectionResultDto>> ExecuteAsync(ListDataConnectionsQuery query, CancellationToken ct = default)
    {
        var connections = await repository.ListAsync(ct);
        return connections.Select(c => new DataConnectionResultDto(
            c.Id.Value,
            c.Name.Value,
            c.Provider.Name,
            c.CreatedAt,
            c.UpdatedAt,
            (c.UpdatedAt ?? c.CreatedAt).ToUnixTimeMilliseconds().ToString())).ToList();
    }
}
