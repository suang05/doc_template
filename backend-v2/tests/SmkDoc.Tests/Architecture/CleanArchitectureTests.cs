using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Common;

namespace SmkDoc.Tests.Architecture;

public class CleanArchitectureTests
{
    [Fact]
    public void Domain_Should_Not_Have_Any_Project_Or_Framework_Dependencies()
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
    public void Application_Should_Only_Depend_On_Domain()
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
    public void Domain_Entities_Should_Be_Sealed_And_Encapsulated()
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
