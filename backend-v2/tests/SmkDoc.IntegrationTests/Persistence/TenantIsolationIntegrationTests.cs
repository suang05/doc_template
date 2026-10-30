using System;
using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.Persistence.Interceptors;

namespace SmkDoc.IntegrationTests.Persistence;

[Trait("Category", "Integration")]
public sealed class TenantIsolationIntegrationTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 3, 31, 0, 0, 0, TimeSpan.Zero);

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
        var versionFilters = dbContext.Model.FindEntityType(typeof(TemplateVersion))?.GetDeclaredQueryFilters();
        var mappingFilters = dbContext.Model.FindEntityType(typeof(FieldMapping))?.GetDeclaredQueryFilters();
        var datasetFilters = dbContext.Model.FindEntityType(typeof(TemplateDataset))?.GetDeclaredQueryFilters();
        var documentVersionFilters = dbContext.Model.FindEntityType(typeof(DocumentVersion))?.GetDeclaredQueryFilters();

        // Assert
        templateFilters.Should().NotBeNull().And.NotBeEmpty("Template implements IMustHaveProject and must have a global query filter");
        apiKeyFilters.Should().NotBeNull().And.NotBeEmpty("ApiKey implements IMustHaveProject and must have a global query filter");
        companyFilters.Should().NotBeNull().And.BeEmpty("Company is a tenant root entity and must not have a ProjectId query filter");
        versionFilters.Should().NotBeNull().And.NotBeEmpty("TemplateVersion is a Template child and must have an aligned query filter");
        mappingFilters.Should().NotBeNull().And.NotBeEmpty("FieldMapping is a Template child and must have an aligned query filter");
        datasetFilters.Should().NotBeNull().And.NotBeEmpty("TemplateDataset is a Template child and must have an aligned query filter");
        documentVersionFilters.Should().NotBeNull().And.NotBeEmpty("DocumentVersion is a DocumentRecord child and must have an aligned query filter");
    }

    [Fact]
    public void ModelConfiguration_ChildEntitiesWithDomainAssignedIds_UseValueGeneratedNever()
    {
        // Arrange
        using var dbContext = new AppDbContext(CreateOptions());
        var childTypes = new[]
        {
            typeof(TemplateVersion),
            typeof(FieldMapping),
            typeof(TemplateDataset),
            typeof(DocumentVersion)
        };

        // Act & Assert
        foreach (var childType in childTypes)
        {
            var idProperty = dbContext.Model.FindEntityType(childType)?.FindPrimaryKey()?.Properties.Single();
            idProperty.Should().NotBeNull($"{childType.Name} must define a primary key");
            idProperty!.ValueGenerated.Should().Be(
                Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never,
                $"{childType.Name}.Id is assigned in domain code via Guid.NewGuid() and must use ValueGeneratedNever");
        }
    }

    private static ICurrentProjectContext CreateProjectContext(ProjectId projectId, bool isResolved = true)
    {
        var context = Substitute.For<ICurrentProjectContext>();
        context.IsResolved.Returns(isResolved);
        context.ProjectId.Returns(projectId);
        return context;
    }

    [Fact]
    public void DbContext_Properties_ReflectInjectedProjectContext()
    {
        // Arrange
        var expectedProjectId = ProjectId.New();
        var projectContext = CreateProjectContext(expectedProjectId, isResolved: true);

        // Act
        using var dbContext = new AppDbContext(CreateOptions(), projectContext);

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

        var projectContext = CreateProjectContext(activeTenantId);
        var interceptor = new TenantSecuritySaveChangesInterceptor(projectContext);
        using var dbContext = new AppDbContext(CreateOptions(interceptor), projectContext);

        var crossTenantTemplate = Template.Create(
            foreignTenantId,
            TemplateName.Create("Cross Tenant Template"),
            TemplateSlug.Create("cross-tenant-template"),
            null,
            FixedNow);

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

        var projectContext = CreateProjectContext(activeTenantId);
        var interceptor = new TenantSecuritySaveChangesInterceptor(projectContext);
        using var dbContext = new AppDbContext(CreateOptions(interceptor), projectContext);

        var matchingTemplate = Template.Create(
            activeTenantId,
            TemplateName.Create("Matching Tenant Template"),
            TemplateSlug.Create("matching-tenant-template"),
            null,
            FixedNow);

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

        var projectContext = CreateProjectContext(ProjectId.Empty, isResolved: false);
        var interceptor = new TenantSecuritySaveChangesInterceptor(projectContext);
        using var dbContext = new AppDbContext(CreateOptions(interceptor), projectContext);

        var template = Template.Create(
            foreignTenantId,
            TemplateName.Create("Unresolved Context Template"),
            TemplateSlug.Create("unresolved-context-template"),
            null,
            FixedNow);

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

        var projectContext = CreateProjectContext(activeTenantId);
        var interceptor = new TenantSecuritySaveChangesInterceptor(projectContext);
        using var dbContext = new AppDbContext(CreateOptions(interceptor), projectContext);

        // ApiKey has a private parameterless constructor for ORM materialization with ProjectId = ProjectId.Empty
        var apiKey = (ApiKey)Activator.CreateInstance(typeof(ApiKey), nonPublic: true)!;
        apiKey.ProjectId.IsEmpty.Should().BeTrue("Default instantiated ApiKey starts with empty ProjectId");

        dbContext.ApiKeys.Add(apiKey);

        // Act
        interceptor.ApplyTenantSecurity(dbContext);

        // Assert
        apiKey.ProjectId.Should().Be(activeTenantId, "Empty ProjectId on added entity must be auto-assigned to active tenant");
    }

    [Fact]
    public void Interceptor_WhenTenantResolved_ModifyingCrossTenantEntity_ThrowsInvalidOperationException()
    {
        // Arrange
        var activeTenantId = ProjectId.New();
        var foreignTenantId = ProjectId.New();

        var projectContext = CreateProjectContext(activeTenantId);
        var interceptor = new TenantSecuritySaveChangesInterceptor(projectContext);
        using var dbContext = new AppDbContext(CreateOptions(interceptor), projectContext);

        var crossTenantTemplate = Template.Create(
            foreignTenantId,
            TemplateName.Create("Cross Tenant Template"),
            TemplateSlug.Create("cross-tenant-template"),
            null,
            FixedNow);

        dbContext.Templates.Attach(crossTenantTemplate);
        dbContext.Entry(crossTenantTemplate).State = EntityState.Modified;

        // Act
        var act = () => interceptor.ApplyTenantSecurity(dbContext);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cross-tenant update operation rejected*");
    }

    [Fact]
    public void Interceptor_WhenTenantResolved_DeletingCrossTenantEntity_ThrowsInvalidOperationException()
    {
        // Arrange
        var activeTenantId = ProjectId.New();
        var foreignTenantId = ProjectId.New();

        var projectContext = CreateProjectContext(activeTenantId);
        var interceptor = new TenantSecuritySaveChangesInterceptor(projectContext);
        using var dbContext = new AppDbContext(CreateOptions(interceptor), projectContext);

        var crossTenantTemplate = Template.Create(
            foreignTenantId,
            TemplateName.Create("Cross Tenant Template"),
            TemplateSlug.Create("cross-tenant-template"),
            null,
            FixedNow);

        dbContext.Templates.Attach(crossTenantTemplate);
        dbContext.Entry(crossTenantTemplate).State = EntityState.Deleted;

        // Act
        var act = () => interceptor.ApplyTenantSecurity(dbContext);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cross-tenant delete operation rejected*");
    }

    [Fact]
    public void Interceptor_WhenTenantResolved_AttemptingTenantReassignment_ThrowsInvalidOperationException()
    {
        // Arrange
        var activeTenantId = ProjectId.New();
        var foreignTenantId = ProjectId.New();

        var projectContext = CreateProjectContext(activeTenantId);
        var interceptor = new TenantSecuritySaveChangesInterceptor(projectContext);
        using var dbContext = new AppDbContext(CreateOptions(interceptor), projectContext);

        var matchingTemplate = Template.Create(
            activeTenantId,
            TemplateName.Create("Matching Template"),
            TemplateSlug.Create("matching-template"),
            null,
            FixedNow);

        dbContext.Templates.Attach(matchingTemplate);
        dbContext.Entry(matchingTemplate).Property(nameof(IMustHaveProject.ProjectId)).CurrentValue = foreignTenantId;
        dbContext.Entry(matchingTemplate).Property(nameof(IMustHaveProject.ProjectId)).IsModified = true;

        // Act
        var act = () => interceptor.ApplyTenantSecurity(dbContext);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Tenant reassignment is strictly prohibited*");
    }
}

