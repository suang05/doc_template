using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application;
using Xunit;

namespace SmkDoc.Tests.Api;

public class DependencyInjectionSmokeTests
{
    [Fact]
    public void AllUseCases_MustBeRegisteredInApplicationServices()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();

        var useCaseTypes = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("UseCase"))
            .ToList();

        useCaseTypes.Should().NotBeEmpty("there should be UseCase classes in SmkDoc.Application");

        var missing = new List<string>();
        foreach (var useCaseType in useCaseTypes)
        {
            var isRegistered = services.Any(sd => sd.ServiceType == useCaseType || sd.ImplementationType == useCaseType);
            if (!isRegistered)
            {
                missing.Add(useCaseType.FullName ?? useCaseType.Name);
            }
        }

        missing.Should().BeEmpty("all UseCase classes in SmkDoc.Application must be registered in DI container");
    }

    [Fact]
    public void AllCommandValidators_MustBeRegisteredInApplicationServices()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();

        var validatorTypes = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)))
            .ToList();

        validatorTypes.Should().NotBeEmpty("there should be FluentValidation validators in SmkDoc.Application");

        var missing = new List<string>();
        foreach (var validatorType in validatorTypes)
        {
            var isRegistered = services.Any(sd => sd.ImplementationType == validatorType);
            if (!isRegistered)
            {
                missing.Add(validatorType.FullName ?? validatorType.Name);
            }
        }

        missing.Should().BeEmpty("all FluentValidation validators in SmkDoc.Application must be registered via AddValidatorsFromAssemblyContaining");
    }
}
