using System;
using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.Persistence.Interceptors;

namespace SmkDoc.IntegrationTests.Persistence;

[Trait("Category", "Integration")]
public class TenantIsolationIntegrationTests
{
    private static DbContextOptions<AppDbContext> CreateOptions(TenantSecuritySaveChangesInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=test_isolation_db;Username=test;Password=test");

        if (interceptor != null)
        {
            builder.AddInterceptors(interceptor);
        }

        return builder.Options;
    }

    [Fact]
    public void ModelConfiguration_IMustHaveProjectEntities_HaveQueryFilterConfigured()
    {
        // Arrange
        using var dbContext = new AppDbContext(CreateOptions());

        // Act
        var templateFilters = dbContext.Model.FindEntityType(typeof(Template))?.GetDeclaredQueryFilters();
        var apiKeyFilters = dbContext.Model.FindEntityType(typeof(ApiKey))?.GetDeclaredQueryFilters();
        var companyFilters = dbContext.Model.FindEntityType(typeof(Company))?.GetDeclaredQueryFilters();

        // Assert
        templateFilters.Should().NotBeNull().And.NotBeEmpty("Template implements IMustHaveProject and must have a global query filter");
        apiKeyFilters.Should().NotBeNull().And.NotBeEmpty("ApiKey implements IMustHaveProject and must have a global query filter");
        companyFilters.Should().NotBeNull().And.BeEmpty("Company is a tenant root entity and must not have a ProjectId query filter");
    }

    [Fact]
    public void DbContext_Properties_ReflectInjectedProjectContext()
    {
        // Arrange
        var expectedProjectId = ProjectId.New();
        var mockContext = new Mock<ICurrentProjectContext>();
        mockContext.Setup(c => c.IsResolved).Returns(true);
        mockContext.Setup(c => c.ProjectId).Returns(expectedProjectId);

        // Act
        using var dbContext = new AppDbContext(CreateOptions(), mockContext.Object);

        // Assert
        dbContext.IsTenantResolved.Should().BeTrue();
        dbContext.CurrentTenantId.Should().Be(expectedProjectId);
    }

    [Fact]
    public void DbContext_WhenProjectContextIsNull_DefaultsToUnresolvedAndEmptyTenant()
    {
        // Arrange & Act
        using var dbContext = new AppDbContext(CreateOptions(), null);

        // Assert
        dbContext.IsTenantResolved.Should().BeFalse();
        dbContext.CurrentTenantId.Should().Be(ProjectId.Empty);
    }

    [Fact]
    public void Interceptor_WhenTenantResolved_AddingCrossTenantEntity_ThrowsInvalidOperationException()
    {
        // Arrange
        var activeTenantId = ProjectId.New();
        var foreignTenantId = ProjectId.New();

        var mockContext = new Mock<ICurrentProjectContext>();
        mockContext.Setup(c => c.IsResolved).Returns(true);
        mockContext.Setup(c => c.ProjectId).Returns(activeTenantId);

        var interceptor = new TenantSecuritySaveChangesInterceptor(mockContext.Object);
        using var dbContext = new AppDbContext(CreateOptions(interceptor), mockContext.Object);

        var crossTenantTemplate = Template.Create(
            foreignTenantId,
            TemplateName.Create("Cross Tenant Template"),
            TemplateSlug.Create("cross-tenant-template"),
            null,
            DateTimeOffset.UtcNow);

        dbContext.Templates.Add(crossTenantTemplate);

        // Act: ApplyTenantSecurity is called during SaveChanges/SaveChangesAsync
        var act = () => interceptor.ApplyTenantSecurity(dbContext);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cross-tenant write operation rejected*");
    }

    [Fact]
    public void Interceptor_WhenTenantResolved_AddingEntityWithMatchingProjectId_PermitsExecution()
    {
        // Arrange
        var activeTenantId = ProjectId.New();

        var mockContext = new Mock<ICurrentProjectContext>();
        mockContext.Setup(c => c.IsResolved).Returns(true);
        mockContext.Setup(c => c.ProjectId).Returns(activeTenantId);

        var interceptor = new TenantSecuritySaveChangesInterceptor(mockContext.Object);
        using var dbContext = new AppDbContext(CreateOptions(interceptor), mockContext.Object);

        var matchingTemplate = Template.Create(
            activeTenantId,
            TemplateName.Create("Matching Tenant Template"),
            TemplateSlug.Create("matching-tenant-template"),
            null,
            DateTimeOffset.UtcNow);

        dbContext.Templates.Add(matchingTemplate);

        // Act
        var act = () => interceptor.ApplyTenantSecurity(dbContext);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Interceptor_WhenTenantUnresolved_AddingEntity_DoesNotRejectCrossTenant()
    {
        // Arrange
        var foreignTenantId = ProjectId.New();

        var mockContext = new Mock<ICurrentProjectContext>();
        mockContext.Setup(c => c.IsResolved).Returns(false);
        mockContext.Setup(c => c.ProjectId).Returns(ProjectId.Empty);

        var interceptor = new TenantSecuritySaveChangesInterceptor(mockContext.Object);
        using var dbContext = new AppDbContext(CreateOptions(interceptor), mockContext.Object);

        var template = Template.Create(
            foreignTenantId,
            TemplateName.Create("Unresolved Context Template"),
            TemplateSlug.Create("unresolved-context-template"),
            null,
            DateTimeOffset.UtcNow);

        dbContext.Templates.Add(template);

        // Act
        var act = () => interceptor.ApplyTenantSecurity(dbContext);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Interceptor_WhenTenantResolved_EntityWithEmptyProjectId_IsAutoAssignedActiveTenantId()
    {
        // Arrange
        var activeTenantId = ProjectId.New();

        var mockContext = new Mock<ICurrentProjectContext>();
        mockContext.Setup(c => c.IsResolved).Returns(true);
        mockContext.Setup(c => c.ProjectId).Returns(activeTenantId);

        var interceptor = new TenantSecuritySaveChangesInterceptor(mockContext.Object);
        using var dbContext = new AppDbContext(CreateOptions(interceptor), mockContext.Object);

        // ApiKey has a private parameterless constructor for ORM materialization with ProjectId = ProjectId.Empty
        var apiKey = (ApiKey)Activator.CreateInstance(typeof(ApiKey), nonPublic: true)!;
        apiKey.ProjectId.IsEmpty.Should().BeTrue("Default instantiated ApiKey starts with empty ProjectId");

        dbContext.ApiKeys.Add(apiKey);

        // Act
        interceptor.ApplyTenantSecurity(dbContext);

        // Assert
        apiKey.ProjectId.Should().Be(activeTenantId, "Empty ProjectId on added entity must be auto-assigned to active tenant");
    }
}
