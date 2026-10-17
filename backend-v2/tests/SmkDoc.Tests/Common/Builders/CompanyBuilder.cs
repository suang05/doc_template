using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Builders;

public class CompanyBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Acme Corp";
    private bool _isActive = true;
    private DateTimeOffset _now = TestConstants.BaselineTime;

    public static CompanyBuilder ACompany() => new();

    public static Company Default() => ACompany().Build();

    public static Company Create(
        Guid? id = null,
        string name = "Acme Corp",
        DateTimeOffset? now = null,
        bool isActive = true)
    {
        var builder = new CompanyBuilder();
        if (id.HasValue) builder.WithId(id.Value);
        builder.WithName(name);
        if (now.HasValue) builder.WithTime(now.Value);
        if (!isActive) builder.AsInactive();
        return builder.Build();
    }

    public CompanyBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public CompanyBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CompanyBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    public CompanyBuilder WithTime(DateTimeOffset now)
    {
        _now = now;
        return this;
    }

    public Company Build()
    {
        var company = new Company(
            _id,
            CompanyName.Create(_name),
            _now);

        if (!_isActive)
        {
            company.Deactivate(_now);
        }

        return company;
    }
}
