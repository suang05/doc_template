using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;

public record ListDataConnectionsQuery;

public sealed class ListDataConnectionsUseCase(
    IDataConnectionRepository repository) : IUseCase<ListDataConnectionsQuery, List<DataConnectionDto>>
{
    private readonly IDataConnectionRepository _repository = repository;

    public async Task<List<DataConnectionDto>> ExecuteAsync(ListDataConnectionsQuery query, CancellationToken ct = default)
    {
        var connections = await _repository.ListAsync(ct);
        return connections.Select(c => new DataConnectionDto
        {
            Id = c.Id,
            Name = c.Name,
            Provider = c.Provider,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        }).ToList();
    }
}
