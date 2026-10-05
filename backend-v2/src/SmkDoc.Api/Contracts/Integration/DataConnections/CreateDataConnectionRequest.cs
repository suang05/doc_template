namespace SmkDoc.Api.Contracts.Integration.DataConnections;

public record CreateDataConnectionRequest(string Name, string Provider, string ConnectionString);
