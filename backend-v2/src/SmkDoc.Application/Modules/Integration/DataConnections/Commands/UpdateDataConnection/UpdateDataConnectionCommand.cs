namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;

public sealed record UpdateDataConnectionCommand(
    Guid Id,
    string Name,
    string Provider,
    string ConnectionString,
    long ExpectedVersion = 1);
