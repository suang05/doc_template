using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.Persistence.Interceptors;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Infrastructure.Persistence.Interceptors;

public sealed class ConcurrencySaveChangesInterceptorTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=smkdoc_unit_test;Username=postgres;Password=postgres")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void ValidateConcurrency_WhenEntityModifiedAndIfMatchMatchesOriginalValue_ShouldSucceedEvenAfterUpdatedAtChanged()
    {
        // Step 1: Arrange
        using var dbContext = CreateDbContext();
        var concurrencyContext = new ConcurrencyContext();
        var sut = new ConcurrencySaveChangesInterceptor(concurrencyContext);

        Template template = TemplateBuilder.ATemplate().WithName("Old Name").WithSlug("invoice");
        var originalToken = template.GetConcurrencyToken();
        concurrencyContext.IfMatch = $"\"{originalToken}\"";

        dbContext.Attach(template);
        template.UpdateDetails(TemplateName.Create("New Name"), "Billing", template.CreatedAt.AddMinutes(5));

        // Step 2: Act
        var act = () => sut.ValidateConcurrency(dbContext);

        // Step 3: Assert
        act.Should().NotThrow();
        concurrencyContext.IsVerified.Should().BeTrue();
    }

    [Fact]
    public void ValidateConcurrency_WhenEntityModifiedAndIfMatchMismatchesOriginalValue_ShouldThrowPreconditionFailedException()
    {
        // Step 1: Arrange
        using var dbContext = CreateDbContext();
        var concurrencyContext = new ConcurrencyContext();
        var sut = new ConcurrencySaveChangesInterceptor(concurrencyContext);

        Template template = TemplateBuilder.ATemplate().WithName("Old Name").WithSlug("invoice");
        concurrencyContext.IfMatch = "\"stale-token-000\"";

        dbContext.Attach(template);
        template.UpdateDetails(TemplateName.Create("New Name"), "Billing", template.CreatedAt.AddMinutes(5));

        // Step 2: Act
        var act = () => sut.ValidateConcurrency(dbContext);

        // Step 3: Assert
        act.Should().Throw<PreconditionFailedException>();
        concurrencyContext.IsVerified.Should().BeFalse();
    }

    [Fact]
    public void ValidateConcurrency_WhenOnlyChildEntityModified_ShouldIgnoreChildEntityAndNotThrow()
    {
        // Step 1: Arrange
        using var dbContext = CreateDbContext();
        var concurrencyContext = new ConcurrencyContext();
        var sut = new ConcurrencySaveChangesInterceptor(concurrencyContext);

        Template template = TemplateBuilder.ATemplate().WithName("Name").WithSlug("invoice");
        var mapping = FieldMapping.Create(template.Id, "customer_name", "$.customer.name", "Customer Name", true, 0, template.CreatedAt);
        concurrencyContext.IfMatch = "\"unrelated-token\"";

        dbContext.Attach(mapping);
        dbContext.Entry(mapping).State = EntityState.Modified;

        // Step 2: Act
        var act = () => sut.ValidateConcurrency(dbContext);

        // Step 3: Assert
        act.Should().NotThrow();
    }
}
