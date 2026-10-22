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

    public static implicit operator Company(CompanyBuilder builder) => builder.Build();
}
