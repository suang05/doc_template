using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;

public record ListDataConnectionsQuery;

public sealed class ListDataConnectionsUseCase(
    IDataConnectionRepository repository) : IUseCase<ListDataConnectionsQuery, List<DataConnectionDto>>
{
    public async Task<List<DataConnectionDto>> ExecuteAsync(ListDataConnectionsQuery query, CancellationToken ct = default)
    {
        var connections = await repository.ListAsync(ct);
        return connections.Select(c => new DataConnectionDto
        {
            Id = c.Id.Value,
            Name = c.Name.Value,
            Provider = c.Provider.Name,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        }).ToList();
    }
}
