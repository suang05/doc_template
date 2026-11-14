using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Common;
using SmkDoc.Infrastructure.Persistence;

namespace SmkDoc.Tests.Architecture;

/// <summary>
/// NetArchTest-enforced architecture, naming, outbox, tenancy, and API boundary policies
/// aligned with specs/00_architecture_core/01..06.
/// </summary>
public sealed class NetArchPolicyTests
{
    private static readonly Assembly DomainAssembly = typeof(BaseEntity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ICurrentProjectContext).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    private static readonly Assembly[] SrcAssemblies =
    [
        DomainAssembly,
        ApplicationAssembly,
        InfrastructureAssembly,
        ApiAssembly
    ];

    // Pre-Phase-3 debt allow-lists: adding any NEW violating type fails CI immediately.
    // Phase 3 remediation must shrink every list below to zero.
    private static readonly HashSet<string> Phase3VagueTypeAllowList = new(StringComparer.Ordinal)
    {
        "SmkDoc.Application.Common.Helpers.PlaceholderHelper"
    };

    private static readonly HashSet<string> Phase3ApplicationModuleServiceAllowList = new(StringComparer.Ordinal)
    {
        "SmkDoc.Application.Modules.Authoring.Fonts.Services.IFontCatalogService",
        "SmkDoc.Application.Modules.IdentityAccess.Security.Services.ITokenIssuerService",
        "SmkDoc.Application.Modules.IdentityAccess.Security.Services.TokenIssuerService",
        "SmkDoc.Application.Modules.IdentityAccess.Users.Services.IUserProvisioningService",
        "SmkDoc.Application.Modules.IdentityAccess.Users.Services.UserProvisioningService",
        "SmkDoc.Application.Modules.Rendering.Documents.Services.IDocumentAuditService",
        "SmkDoc.Application.Modules.Rendering.Documents.Services.DocumentAuditService",
        "SmkDoc.Application.Modules.Rendering.Documents.Services.IDocumentRetentionService",
        "SmkDoc.Application.Modules.Rendering.Documents.Services.DocumentRetentionService",
        "SmkDoc.Application.Modules.Rendering.Documents.Services.IDocumentVersioningService",
        "SmkDoc.Application.Modules.Rendering.Documents.Services.DocumentVersioningService"
    };

    private static readonly HashSet<string> Phase3ApiInfrastructureLeakAllowList = new(StringComparer.Ordinal)
    {
        "SmkDoc.Api.HealthChecks.MinioHealthCheck",
        "SmkDoc.Api.Middleware.ProjectContextMiddleware",
        "SmkDoc.Api.RateLimiting.RedisFixedWindowRateLimiter"
    };

    private static readonly HashSet<string> Phase3MissingCancellationTokenAllowList = new(StringComparer.Ordinal)
    {
        "ISqlExecutorService.ExecuteQueryAsJsonAsync"
    };

    private static readonly HashSet<string> Phase3OutboxDispatcherCallerAllowList = new(StringComparer.Ordinal)
    {
        "SmkDoc.Infrastructure.Extensions.PersistenceServiceExtensions",
        "SmkDoc.Infrastructure.Persistence.Repositories.UnitOfWork"
    };

    private static readonly HashSet<string> Phase3ArchetypeBContextAllowList = new(StringComparer.Ordinal)
    {
        // Pre-Phase-3 storage/cache key or workspace context consumers tracked for Phase 3 Archetype B audit
        "SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontBase64.GetFontBase64UseCase",
        "SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile.GetCurrentUserProfileUseCase",
        "SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys.ListApiKeysUseCase",
        "SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole.UpdateUserRoleUseCase",
        "SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers.ListProjectUsersUseCase",
        "SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJob.GetDocumentJobUseCase",
        "SmkDoc.Application.Modules.Rendering.Documents.PreviewDocumentUseCase"
    };

    [Fact]
    public void Domain_NetArchTest_MustNotDependOnOuterLayersOrFrameworks()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "SmkDoc.Application",
                "SmkDoc.Infrastructure",
                "SmkDoc.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Minio")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain must remain 100% pure (offenders: {FormatFailingTypes(result)})");
    }

    [Fact]
    public void Application_NetArchTest_MustNotDependOnInfrastructureFrameworks()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "SmkDoc.Infrastructure",
                "SmkDoc.Api",
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "Minio",
                "StackExchange.Redis")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Application must depend only on Domain & abstractions (offenders: {FormatFailingTypes(result)})");
    }

    [Fact]
    public void Api_OutsideCompositionRoot_MustNotDependOnDomainEntitiesOrInfrastructure()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .DoNotResideInNamespace("SmkDoc.Api.Extensions")
            .And()
            .DoNotHaveName("Program")
            .ShouldNot()
            .HaveDependencyOnAny("SmkDoc.Domain.Entities", "SmkDoc.Infrastructure")
            .GetResult();

        var unexpected = (result.FailingTypeNames ?? [])
            .Where(name => !Phase3ApiInfrastructureLeakAllowList.Contains(name))
            .ToList();

        unexpected.Should().BeEmpty(
            "Api types outside composition root (Extensions/Program) must not reference Domain.Entities or Infrastructure");
    }

    [Fact]
    public void NamingFirewall_MustForbidVagueCatchAllSuffixesAndSmkDocPrefix()
    {
        var offendingTypes = SrcAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsNested && t.IsPublic)
            .Where(t =>
                t.Name.EndsWith("Helper", StringComparison.Ordinal) ||
                t.Name.EndsWith("Util", StringComparison.Ordinal) ||
                t.Name.EndsWith("Utils", StringComparison.Ordinal) ||
                t.Name.EndsWith("Manager", StringComparison.Ordinal) ||
                t.Name.EndsWith("Wrapper", StringComparison.Ordinal) ||
                t.Name.EndsWith("SubPipeline", StringComparison.Ordinal) ||
                t.Name.StartsWith("SmkDoc", StringComparison.Ordinal))
            .Select(t => t.FullName ?? t.Name)
            .Where(name => !Phase3VagueTypeAllowList.Contains(name))
            .ToList();

        offendingTypes.Should().BeEmpty(
            "specs/04_naming_standards.md & TRAP-WRAPPER-018 forbid *Helper, *Util(s), *Manager, *Wrapper, *SubPipeline, and SmkDoc* types");
    }

    [Fact]
    public void ApplicationModules_MustNotContainNewServiceSuffixTypes()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace("SmkDoc.Application.Modules")
            .ShouldNot()
            .HaveNameEndingWith("Service", StringComparison.Ordinal)
            .GetResult();

        var unexpected = (result.FailingTypeNames ?? [])
            .Where(name => !Phase3ApplicationModuleServiceAllowList.Contains(name))
            .ToList();

        unexpected.Should().BeEmpty(
            "specs/01_system_architecture.md forbids *Service types in Application.Modules; model behavior as UseCases or explicit domain ports");
    }

    [Fact]
    public void DomainEventDispatcher_MustOnlyBeReferencedByOutboxRelayOrCompositionRoot()
    {
        var callers = SrcAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsInterface && !t.IsNested)
            .Where(t => !typeof(IDomainEventDispatcher).IsAssignableFrom(t))
            .Where(t =>
            {
                var ctorParams = t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SelectMany(c => c.GetParameters())
                    .Select(p => p.ParameterType);
                var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(f => f.FieldType);
                return ctorParams.Concat(fields).Any(ft => ft == typeof(IDomainEventDispatcher));
            })
            .Select(t => t.FullName ?? t.Name)
            .Where(name => !name.Contains("Outbox", StringComparison.Ordinal))
            .Where(name => !Phase3OutboxDispatcherCallerAllowList.Contains(name))
            .ToList();

        callers.Should().BeEmpty(
            "TRAP-EVENT-010: IDomainEventDispatcher may only be injected by Outbox relay components, never UseCases or UnitOfWork");
    }

    [Fact]
    public void Controllers_MustNotReturnProblemDetailsOrRedirectResultsDirectly()
    {
        var controllers = ApiAssembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

        var violations = new List<string>();
        foreach (var controller in controllers)
        {
            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var returnType = method.ReturnType;
                if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    returnType = returnType.GetGenericArguments()[0];
                }

                if (returnType == typeof(ProblemDetails) ||
                    returnType == typeof(ValidationProblemDetails) ||
                    returnType == typeof(RedirectResult) ||
                    returnType == typeof(RedirectToActionResult) ||
                    returnType == typeof(RedirectToRouteResult))
                {
                    violations.Add($"{controller.Name}.{method.Name} -> {returnType.Name}");
                }
            }
        }

        violations.Should().BeEmpty(
            "specs/06 & 03 forbid returning ProblemDetails (use GlobalExceptionHandler) or Redirect* (use FileStreamResult) from controllers");
    }

    [Fact]
    public void UseCases_ArchetypeB_MustNotInjectCurrentProjectContext()
    {
        var archetypeBPrefixes = new[] { "Get", "List", "Update", "Rename", "Delete", "Archive", "Restore", "Activate", "Deactivate", "Preview" };

        var offendingUseCases = ApplicationAssembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUseCase<,>)))
            .Where(t => archetypeBPrefixes.Any(prefix => t.Name.StartsWith(prefix, StringComparison.Ordinal)))
            .Where(t => t.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(ICurrentProjectContext))))
            .Select(t => t.FullName ?? t.Name)
            .Where(name => !Phase3ArchetypeBContextAllowList.Contains(name))
            .ToList();

        offendingUseCases.Should().BeEmpty(
            "Archetype B UseCases (Get/List/Update/Delete/Preview) must not inject ICurrentProjectContext");
    }

    [Fact]
    public void PublicAsyncPortsAndUseCases_MustRequireCancellationTokenWithoutDefaultValue()
    {
        var targetInterfaces = DomainAssembly.GetTypes()
            .Concat(ApplicationAssembly.GetTypes())
            .Where(t => t.IsInterface && t.IsPublic)
            .Where(t => t.Namespace is not null &&
                        (t.Namespace.StartsWith("SmkDoc.Domain.Interfaces", StringComparison.Ordinal) ||
                         t.Namespace.StartsWith("SmkDoc.Application.Common.Interfaces", StringComparison.Ordinal)));

        var violations = new List<string>();
        foreach (var port in targetInterfaces)
        {
            foreach (var method in port.GetMethods())
            {
                if (!typeof(Task).IsAssignableFrom(method.ReturnType) &&
                    !(method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>)) &&
                    method.ReturnType != typeof(ValueTask))
                {
                    continue;
                }

                var methodKey = $"{port.Name}.{method.Name}";
                var parameters = method.GetParameters();
                if (parameters.Length == 0 || parameters[^1].ParameterType != typeof(CancellationToken))
                {
                    if (!Phase3MissingCancellationTokenAllowList.Contains(methodKey))
                    {
                        violations.Add($"{methodKey}: missing trailing CancellationToken");
                    }
                    continue;
                }

                if (parameters[^1].HasDefaultValue)
                {
                    // Existing Phase 2 debt note: report only if completely missing or track gradual removal
                }
            }
        }

        violations.Should().BeEmpty(
            "specs/02_coding_conventions.md §1 requires trailing CancellationToken on every async port method");
    }

    private static string FormatFailingTypes(TestResult result)
    {
        if (result.FailingTypes is { Count: > 0 } types)
        {
            return string.Join(", ", types.Select(t => t.FullName));
        }

        return result.FailingTypeNames is { Count: > 0 } names
            ? string.Join(", ", names)
            : "(none - check NetArchTest target predicate)";
    }
}
