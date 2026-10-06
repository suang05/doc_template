using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Builders;

public class UserBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _email = "user@sammakorn.co.th";
    private string _passwordHash = "hashed_default_password";
    private string _firstName = "สมชาย";
    private string _lastName = "ใจดี";
    private SystemRole _systemRole = SystemRole.Member;
    private bool _isActive = true;
    private DateTimeOffset _now = TestConstants.BaselineTime;

    public static UserBuilder AUser() => new();

    public UserBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public UserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public UserBuilder WithPasswordHash(string hash)
    {
        _passwordHash = hash;
        return this;
    }

    public UserBuilder WithName(string firstName, string lastName)
    {
        _firstName = firstName;
        _lastName = lastName;
        return this;
    }

    public UserBuilder WithSystemRole(SystemRole role)
    {
        _systemRole = role;
        return this;
    }

    public UserBuilder WithTime(DateTimeOffset time)
    {
        _now = time;
        return this;
    }

    public UserBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    public User Build()
    {
        var user = new User(_id, EmailAddress.Create(_email), _passwordHash, _firstName, _lastName, _systemRole, _now);
        if (!_isActive)
        {
            user.Deactivate(_now);
        }
        return user;
    }
}
