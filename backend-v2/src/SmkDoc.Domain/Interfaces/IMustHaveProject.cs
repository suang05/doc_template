using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

public interface IMustHaveProject
{
    ProjectId ProjectId { get; }
}
