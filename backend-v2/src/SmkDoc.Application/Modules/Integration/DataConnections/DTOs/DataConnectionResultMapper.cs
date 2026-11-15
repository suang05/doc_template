using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.Integration.DataConnections.DTOs;

/// <summary>
/// Maps <see cref="DataConnection"/> entities to <see cref="DataConnectionResultDto"/>.
/// </summary>
public static class DataConnectionResultMapper
{
    public static DataConnectionResultDto ToResultDto(this DataConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return new DataConnectionResultDto(
            connection.Id.Value,
            connection.Name.Value,
            connection.Provider.Name,
            connection.CreatedAt,
            connection.UpdatedAt,
            connection.Version);
    }
}
