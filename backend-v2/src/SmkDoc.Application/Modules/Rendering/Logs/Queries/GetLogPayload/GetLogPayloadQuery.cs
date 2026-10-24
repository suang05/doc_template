namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogPayload;

/// <summary>
/// Query to retrieve and de-reference the generation payload for an audit log.
/// </summary>
public sealed record class GetLogPayloadQuery(Guid LogId);
