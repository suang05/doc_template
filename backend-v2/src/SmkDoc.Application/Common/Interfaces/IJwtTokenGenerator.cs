using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user, Guid projectId, IEnumerable<string> roles);
}
