namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Optional contract for created-resource DTOs allowing durable idempotency records to store <c>result_resource_id</c>.
/// </summary>
public interface IHasResourceId
{
    Guid ResourceId { get; }
}
