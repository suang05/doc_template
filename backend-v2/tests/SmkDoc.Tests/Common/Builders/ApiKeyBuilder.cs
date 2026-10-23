using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Common.Builders;

public class ApiKeyBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _projectId = TestConstants.DefaultProjectId;
    private string _name = "Test Key";
    private string? _callerApp = "TestApp";
    private Sha256Hash _keyHash = Sha256Hash.Create(new string('a', 64));
    private ExpirationPolicy _expiration = ExpirationPolicy.Never;
    private DateTimeOffset _now = TestConstants.BaselineTime;

    public static ApiKeyBuilder AKey() => new();

    public static ApiKey Default() => AKey().Build();


    public ApiKeyBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public ApiKeyBuilder WithProjectId(Guid projectId)
    {
        _projectId = projectId;
        return this;
    }

    public ApiKeyBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ApiKeyBuilder WithCallerApp(string? callerApp)
    {
        _callerApp = callerApp;
        return this;
    }

    public ApiKeyBuilder WithKeyHash(Sha256Hash keyHash)
    {
        _keyHash = keyHash;
        return this;
    }

    public ApiKeyBuilder WithExpiration(ExpirationPolicy expiration)
    {
        _expiration = expiration;
        return this;
    }

    public ApiKeyBuilder WithTime(DateTimeOffset now)
    {
        _now = now;
        return this;
    }

    public ApiKey Build()
    {
        return new ApiKey(
            new ApiKeyId(_id),
            new ProjectId(_projectId),
            ApiKeyName.Create(_name),
            _callerApp,
            _keyHash,
            _expiration,
            _now);
    }

    public static implicit operator ApiKey(ApiKeyBuilder builder) => builder.Build();
}
