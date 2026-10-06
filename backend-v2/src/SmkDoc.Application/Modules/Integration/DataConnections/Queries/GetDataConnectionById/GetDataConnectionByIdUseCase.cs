using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Queries.GetDataConnectionById;

public record GetDataConnectionByIdQuery(Guid Id);

public sealed class GetDataConnectionByIdUseCase(
    IDataConnectionRepository repository) : IUseCase<GetDataConnectionByIdQuery, DataConnectionDto?>
{
    private readonly IDataConnectionRepository _repository = repository;

    public async Task<DataConnectionDto?> ExecuteAsync(GetDataConnectionByIdQuery query, CancellationToken ct = default)
    {
        var c = await _repository.GetByIdAsync(query.Id, ct);
        if (c == null) return null;

        return new DataConnectionDto
        {
            Id = c.Id,
            Name = c.Name,
            Provider = c.Provider.Name,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }
}
