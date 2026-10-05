namespace SmkDoc.Api.Contracts.Integration.DataConnections;

public record UpdateDataConnectionRequest(string Name, string Provider, string ConnectionString);
