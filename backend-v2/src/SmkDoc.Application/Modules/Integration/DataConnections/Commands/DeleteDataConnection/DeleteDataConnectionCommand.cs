namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;

public sealed record DeleteDataConnectionCommand(
    Guid Id,
    long ExpectedVersion = 1);
