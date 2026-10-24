using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Common;
using SmkDoc.Infrastructure.Persistence;

namespace SmkDoc.Tests.Architecture;

public sealed class CleanArchitectureTests
{
    private static readonly HashSet<Type> SpecializedUseCases =
    [
        typeof(SmkDoc.Application.Modules.Authoring.Templates.HtmlStudioUseCase),
        typeof(SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml.SaveTemplateHtmlUseCase),
        typeof(SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml.ValidateTemplateHtmlUseCase),
        typeof(SmkDoc.Application.Modules.Rendering.Documents.RenderStatelessDocumentUseCase),
        typeof(SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping.PreviewMappingUseCase),
        typeof(SmkDoc.Application.Modules.Authoring.Schemas.ValidateStandaloneSchemaUseCase)
    ];

    [Fact]
    public void Domain_Should_Not_HaveDependencyOn_OtherProjects()
    {
        // Arrange
        var domainAssembly = typeof(BaseEntity).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();
        var referencedNames = referencedAssemblies
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        // Assert
        referencedNames.Should().NotBeEmpty();
        referencedNames.Should().NotContain(name => name.StartsWith("SmkDoc.Application", StringComparison.OrdinalIgnoreCase));
        referencedNames.Should().NotContain(name => name.StartsWith("SmkDoc.Infrastructure", StringComparison.OrdinalIgnoreCase));
        referencedNames.Should().NotContain(name => name.StartsWith("SmkDoc.Api", StringComparison.OrdinalIgnoreCase));
        referencedNames.Should().NotContain(name => name.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));
        referencedNames.Should().NotContain(name => name.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase));
        referencedNames.Should().OnlyContain(name =>
            name.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("netstandard", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Application_Should_Not_HaveDependencyOn_Infrastructure_Or_Api()
    {
        // Arrange
        var appAssembly = typeof(IExecutionContext).Assembly;
        var referencedAssemblies = appAssembly.GetReferencedAssemblies();
        var referencedNames = referencedAssemblies
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        // Assert
        referencedNames.Should().NotBeEmpty();
        referencedNames.Should().NotContain(name => name.StartsWith("SmkDoc.Infrastructure", StringComparison.OrdinalIgnoreCase));
        referencedNames.Should().NotContain(name => name.StartsWith("SmkDoc.Api", StringComparison.OrdinalIgnoreCase));
        referencedNames.Should().NotContain(name => name.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));

        var smkProjectReferences = referencedNames
            .Where(name => name.StartsWith("SmkDoc.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        smkProjectReferences.Should().ContainSingle().Which.Should().Be("SmkDoc.Domain");
    }

    [Fact]
    public void Infrastructure_Should_Not_HaveDependencyOn_Api()
    {
        // Arrange
        var infraAssembly = typeof(AppDbContext).Assembly;
        var referencedAssemblies = infraAssembly.GetReferencedAssemblies();
        var referencedNames = referencedAssemblies
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        // Assert
        referencedNames.Should().NotBeEmpty();
        referencedNames.Should().NotContain(name => name.StartsWith("SmkDoc.Api", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DomainEntities_Should_Have_PrivateSetters_Or_EncapsulatedState()
    {
        // Arrange
        var domainAssembly = typeof(BaseEntity).Assembly;
        var entityTypes = domainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "SmkDoc.Domain.Entities")
            .Where(IsSubclassOfBaseEntity)
            .ToList();

        // Assert
        entityTypes.Should().NotBeEmpty();

        foreach (var entityType in entityTypes)
        {
            entityType.IsSealed.Should().BeTrue($"Domain entity '{entityType.Name}' must be sealed");

            var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                var setter = property.SetMethod;
                if (setter != null)
                {
                    setter.IsPublic.Should().BeFalse(
                        $"Property '{property.Name}' on entity '{entityType.Name}' must not have a public setter");
                }
            }
        }
    }

    [Fact]
    public void UseCases_Should_Implement_IUseCaseInterface()
    {
        // Arrange
        var appAssembly = typeof(IExecutionContext).Assembly;
        var useCaseTypes = appAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("UseCase", StringComparison.OrdinalIgnoreCase))
            .ToList();

        useCaseTypes.Should().NotBeEmpty();

        var nonConformingUseCases = new List<string>();

        foreach (var useCase in useCaseTypes)
        {
            if (SpecializedUseCases.Contains(useCase))
            {
                continue;
            }

            var implementsUseCase = useCase.GetInterfaces().Any(i =>
                i.IsGenericType && (
                    i.GetGenericTypeDefinition() == typeof(IUseCase<,>) ||
                    i.GetGenericTypeDefinition() == typeof(IUseCase<>)
                ));

            if (!implementsUseCase)
            {
                nonConformingUseCases.Add(useCase.FullName ?? useCase.Name);
            }
        }

        // Assert
        nonConformingUseCases.Should().BeEmpty(
            $"all standard CQRS UseCase classes must implement IUseCase<,> or IUseCase<>. Non-conforming: {string.Join(", ", nonConformingUseCases)}");
    }

    [Fact]
    public void Controllers_Should_Not_Reference_DbContext_Or_Repositories_Directly()
    {
        // Arrange
        var apiAssembly = typeof(SmkDoc.Api.Controllers.Rendering.DocumentController).Assembly;
        var controllerTypes = apiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

        // Assert
        controllerTypes.Should().NotBeEmpty();

        foreach (var controller in controllerTypes)
        {
            var constructors = controller.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in constructors)
            {
                foreach (var parameter in ctor.GetParameters())
                {
                    var paramType = parameter.ParameterType;

                    paramType.Name.Should().NotBe("AppDbContext",
                        $"Controller '{controller.Name}' must not inject AppDbContext");

                    typeof(Microsoft.EntityFrameworkCore.DbContext).IsAssignableFrom(paramType).Should().BeFalse(
                        $"Controller '{controller.Name}' must not inject any DbContext derivative");

                    IsRepositoryType(paramType).Should().BeFalse(
                        $"Controller '{controller.Name}' parameter '{parameter.Name}' of type '{paramType.Name}' violates architectural boundary: direct repository injection is prohibited");
                }
            }
        }
    }

    [Fact]
    public void Application_UseCases_Should_Not_Inject_Child_TemplateVersion_Repository()
    {
        // Arrange
        var appAssembly = typeof(IExecutionContext).Assembly;
        var useCaseTypes = appAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("UseCase", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert
        useCaseTypes.Should().NotBeEmpty();

        var prohibitedType = typeof(IRepository<SmkDoc.Domain.Entities.TemplateVersion>);

        foreach (var useCase in useCaseTypes)
        {
            var constructors = useCase.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var ctor in constructors)
            {
                foreach (var parameter in ctor.GetParameters())
                {
                    parameter.ParameterType.Should().NotBe(
                        prohibitedType,
                        $"UseCase '{useCase.Name}' parameter '{parameter.Name}' must not inject child entity repository '{prohibitedType.Name}'. Access TemplateVersion through ITemplateRepository or Template aggregate root.");
                }
            }
        }
    }

    [Theory]
    [InlineData(typeof(SmkDoc.Domain.Entities.Template))]
    [InlineData(typeof(SmkDoc.Domain.Entities.ApiKey))]
    [InlineData(typeof(SmkDoc.Domain.Entities.DataConnection))]
    [InlineData(typeof(SmkDoc.Domain.Entities.Dataset))]
    [InlineData(typeof(SmkDoc.Domain.Entities.Document))]
    [InlineData(typeof(SmkDoc.Domain.Entities.GenerationLog))]
    public void Tenant_Entities_Must_Implement_IMustHaveProject(Type entityType)
    {
        // Assert
        typeof(SmkDoc.Domain.Interfaces.IMustHaveProject).IsAssignableFrom(entityType)
            .Should().BeTrue($"Entity '{entityType.Name}' must implement IMustHaveProject for tenant isolation.");

        var projectIdProp = entityType.GetProperty("ProjectId", BindingFlags.Public | BindingFlags.Instance);
        projectIdProp.Should().NotBeNull($"Entity '{entityType.Name}' must expose public ProjectId property.");
        projectIdProp!.PropertyType.Should().Be(typeof(SmkDoc.Domain.ValueObjects.Ids.ProjectId));
        projectIdProp.GetSetMethod(nonPublic: true).Should().NotBeNull();
        projectIdProp.GetSetMethod(nonPublic: false).Should().BeNull($"Entity '{entityType.Name}' ProjectId setter must be private.");
    }

    private static bool IsSubclassOfBaseEntity(Type type)
    {
        var current = type.BaseType;
        while (current != null && current != typeof(object))
        {
            if (current == typeof(BaseEntity))
            {
                return true;
            }

            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(BaseEntity<>))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool IsRepositoryType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition().Name.Contains("IRepository", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (type.Name.EndsWith("Repository", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
