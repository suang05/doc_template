using SmkDoc.Domain.Enums;

namespace SmkDoc.Domain.Entities;

public class UserProjectRole
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public RoleType Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public User? User { get; set; }
    public Project? Project { get; set; }
}
