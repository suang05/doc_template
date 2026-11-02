namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;

public sealed record CreateDataConnectionCommand(
    string Name,
    string Provider,
    string ConnectionString);
