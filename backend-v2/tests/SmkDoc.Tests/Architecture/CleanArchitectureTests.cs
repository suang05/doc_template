using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Middleware;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Common;
using SmkDoc.Infrastructure.Persistence;

namespace SmkDoc.Tests.Architecture;

public sealed class CleanArchitectureTests
{

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
        var appAssembly = typeof(ICurrentProjectContext).Assembly;
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
        var appAssembly = typeof(ICurrentProjectContext).Assembly;
        var useCaseTypes = appAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("UseCase", StringComparison.OrdinalIgnoreCase))
            .ToList();

        useCaseTypes.Should().NotBeEmpty();

        var nonConformingUseCases = new List<string>();

        foreach (var useCase in useCaseTypes)
        {
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
        var apiAssembly = typeof(SmkDoc.Api.Controllers.Rendering.DocumentGenerationController).Assembly;
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
    public void Application_Classes_Should_Not_Inject_Generic_Repository()
    {
        // Arrange
        var appAssembly = typeof(ICurrentProjectContext).Assembly;
        var appTypes = appAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .ToList();

        // Assert
        appTypes.Should().NotBeEmpty();

        foreach (var type in appTypes)
        {
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var ctor in constructors)
            {
                foreach (var parameter in ctor.GetParameters())
                {
                    var paramType = parameter.ParameterType;
                    var isGenericRepo = paramType.IsGenericType && paramType.Name.StartsWith("IRepository", StringComparison.OrdinalIgnoreCase);

                    isGenericRepo.Should().BeFalse(
                        $"Class '{type.Name}' parameter '{parameter.Name}' must not inject generic repository '{paramType.Name}'. Consume explicit domain repository interfaces instead.");
                }
            }
        }
    }

    [Fact]
    public void Application_Classes_Should_Not_Inject_Child_Entity_Repositories()
    {
        // Arrange
        var appAssembly = typeof(ICurrentProjectContext).Assembly;
        var appTypes = appAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .ToList();

        var prohibitedRepositories = new[] { "DocumentVersionRepository", "TemplateVersionRepository", "FieldMappingRepository", "TemplateDatasetRepository" };

        // Assert
        appTypes.Should().NotBeEmpty();

        foreach (var type in appTypes)
        {
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var ctor in constructors)
            {
                foreach (var parameter in ctor.GetParameters())
                {
                    var paramTypeName = parameter.ParameterType.Name;
                    foreach (var prohibited in prohibitedRepositories)
                    {
                        paramTypeName.Should().NotContain(
                            prohibited,
                            $"Class '{type.Name}' parameter '{parameter.Name}' must not inject child entity repository '{paramTypeName}'. Access child entities through aggregate roots.");
                    }
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

    [Fact]
    public void LegacyExecutionContext_Must_Be_Retired_Across_All_Assemblies()
    {
        var appAssembly = typeof(ICurrentProjectContext).Assembly;
        var infraAssembly = typeof(AppDbContext).Assembly;

        appAssembly.GetType("SmkDoc.Application.Common.Interfaces.IExecutionContext")
            .Should().BeNull("IExecutionContext was retired in favor of unified ICurrentProjectContext");
        infraAssembly.GetType("SmkDoc.Infrastructure.Contexts.ExecutionContextImpl")
            .Should().BeNull("ExecutionContextImpl was retired in favor of unified CurrentProjectContext");
    }

    [Fact]
    public void UseCases_Should_Be_Sealed_Have_Pure_Primary_Constructors_Zero_Direct_Validators_And_Mandatory_TimeProvider()
    {
        var appAssembly = typeof(ICurrentProjectContext).Assembly;
        var useCaseTypes = appAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => !t.IsGenericTypeDefinition || (t != typeof(ValidatingUseCaseDecorator<,>) && t != typeof(ValidatingUseCaseDecorator<>)))
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && (
                    i.GetGenericTypeDefinition() == typeof(IUseCase<,>) ||
                    i.GetGenericTypeDefinition() == typeof(IUseCase<>))))
            .ToList();

        useCaseTypes.Should().NotBeEmpty();

        foreach (var useCaseType in useCaseTypes)
        {
            useCaseType.IsSealed.Should().BeTrue(
                $"UseCase '{useCaseType.FullName}' must be sealed.");

            var manualInstanceFields = useCaseType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(f => !f.Name.StartsWith('<'))
                .ToList();

            manualInstanceFields.Should().BeEmpty(
                $"UseCase '{useCaseType.Name}' must use pure C# primary constructor capture without manual instance fields ({string.Join(", ", manualInstanceFields.Select(f => f.Name))}).");

            var constructors = useCaseType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            constructors.Should().ContainSingle(
                $"UseCase '{useCaseType.Name}' must declare a single public primary constructor.");

            foreach (var parameter in constructors[0].GetParameters())
            {
                typeof(IValidator).IsAssignableFrom(parameter.ParameterType).Should().BeFalse(
                    $"UseCase '{useCaseType.Name}' must not inject '{parameter.ParameterType.Name}' directly; validation runs through ValidatingUseCaseDecorator.");

                if (parameter.ParameterType == typeof(TimeProvider))
                {
                    parameter.HasDefaultValue.Should().BeFalse(
                        $"UseCase '{useCaseType.Name}' parameter '{parameter.Name}' of type TimeProvider must be mandatory (no optional null default).");
                }
            }
        }
    }

    [Fact]
    public void Commands_Queries_And_Validators_Should_Be_Sealed()
    {
        var appAssembly = typeof(ICurrentProjectContext).Assembly;
        var cqrsAndValidatorTypes = appAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t =>
                t.Name.EndsWith("Command", StringComparison.Ordinal) ||
                t.Name.EndsWith("Query", StringComparison.Ordinal) ||
                t.Name.EndsWith("Validator", StringComparison.Ordinal) ||
                typeof(IValidator).IsAssignableFrom(t))
            .ToList();

        cqrsAndValidatorTypes.Should().NotBeEmpty();

        foreach (var type in cqrsAndValidatorTypes)
        {
            type.IsSealed.Should().BeTrue($"CQRS/Validator type '{type.FullName}' must be sealed.");
        }
    }

    [Fact]
    public void Every_Active_Command_Should_Have_A_Dedicated_Validator()
    {
        var appAssembly = typeof(ICurrentProjectContext).Assembly;
        var allTypes = appAssembly.GetTypes();

        var commandTypes = allTypes
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Command", StringComparison.Ordinal))
            .ToList();

        commandTypes.Should().NotBeEmpty();

        var missingValidators = new List<string>();
        foreach (var commandType in commandTypes)
        {
            var expectedValidatorInterface = typeof(IValidator<>).MakeGenericType(commandType);
            var hasValidator = allTypes.Any(t =>
                t.IsClass && !t.IsAbstract && expectedValidatorInterface.IsAssignableFrom(t));

            if (!hasValidator)
            {
                missingValidators.Add(commandType.FullName ?? commandType.Name);
            }
        }

        missingValidators.Should().BeEmpty(
            $"every Command in SmkDoc.Application must have a dedicated IValidator<TCommand> implementation. Missing: {string.Join(", ", missingValidators)}");
    }

    [Fact]
    public void Validators_Should_Be_CoLocated_In_Same_Slice_Namespace_As_Their_Command_Or_Query()
    {
        var appAssembly = typeof(ICurrentProjectContext).Assembly;
        var validatorTypes = appAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IValidator).IsAssignableFrom(t))
            .ToList();

        validatorTypes.Should().NotBeEmpty();

        var violations = new List<string>();
        foreach (var validatorType in validatorTypes)
        {
            var validatedInterfaces = validatorType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
                .ToList();

            validatedInterfaces.Should().ContainSingle(
                $"Validator '{validatorType.FullName}' must validate exactly one Command or Query type.");

            var requestType = validatedInterfaces[0].GetGenericArguments()[0];
            if (!string.Equals(validatorType.Namespace, requestType.Namespace, StringComparison.Ordinal))
            {
                violations.Add($"{validatorType.FullName} validates {requestType.FullName} (mismatched namespace)");
            }

            if (validatorType.Namespace?.EndsWith(".Validators", StringComparison.Ordinal) == true)
            {
                violations.Add($"{validatorType.FullName} resides in a detached .Validators namespace");
            }
        }

        violations.Should().BeEmpty(
            $"every IValidator<T> must be co-located inside the exact same vertical slice namespace as T (no detached Validators/ buckets). Violations: {string.Join("; ", violations)}");
    }

    [Fact]
    public void Application_Module_Files_Should_Contain_Single_Primary_Type_Matching_FileName_Except_Dto_Files()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        string? modulesRoot = null;
        while (currentDirectory is not null)
        {
            var candidate = Path.Combine(currentDirectory.FullName, "src", "SmkDoc.Application", "Modules");
            if (Directory.Exists(candidate))
            {
                modulesRoot = candidate;
                break;
            }
            currentDirectory = currentDirectory.Parent;
        }

        modulesRoot.Should().NotBeNull("src/SmkDoc.Application/Modules directory must be locatable from test output directory.");

        var topLevelTypeRegex = new Regex(
            @"^(?:public|internal)\s+(?:(?:static|sealed|abstract|partial|readonly)\s+)*(?:record(?:\s+(?:class|struct))?|class|interface|enum|struct)\s+(?<Name>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        var violations = new List<string>();

        foreach (var filePath in Directory.EnumerateFiles(modulesRoot!, "*.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(modulesRoot!, filePath).Replace('\\', '/');
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

            var isDtoGroupingFile =
                relativePath.Contains("/DTOs/", StringComparison.Ordinal) &&
                (fileNameWithoutExtension.EndsWith("Dtos", StringComparison.Ordinal) ||
                 fileNameWithoutExtension.EndsWith("Dto", StringComparison.Ordinal));

            if (isDtoGroupingFile)
            {
                continue;
            }

            var source = File.ReadAllText(filePath);
            var stripped = Regex.Replace(source, @"/\*[\s\S]*?\*/|//.*$", string.Empty, RegexOptions.Multiline);
            var declaredTypes = topLevelTypeRegex.Matches(stripped)
                .Select(m => m.Groups["Name"].Value)
                .ToList();

            if (declaredTypes.Count != 1)
            {
                violations.Add($"{relativePath} declares {declaredTypes.Count} primary types [{string.Join(", ", declaredTypes)}]");
                continue;
            }

            if (!string.Equals(declaredTypes[0], fileNameWithoutExtension, StringComparison.Ordinal))
            {
                violations.Add($"{relativePath} declares '{declaredTypes[0]}' which does not match file name '{fileNameWithoutExtension}.cs'");
            }
        }

        violations.Should().BeEmpty(
            $"every non-DTO file under SmkDoc.Application/Modules must declare exactly 1 primary type matching {{TypeName}}.cs ([RULE-CONV-005] / [RULE-STRUCT-004]). Violations: {string.Join("; ", violations)}");
    }

    [Fact]
    public void Api_Controllers_And_Middleware_Should_Be_Sealed_And_Not_Depend_On_Concrete_UseCases()
    {
        var apiAssembly = typeof(SmkDoc.Api.Controllers.Rendering.DocumentGenerationController).Assembly;
        var boundaryTypes = apiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .Append(typeof(ProjectContextMiddleware))
            .ToList();

        boundaryTypes.Should().NotBeEmpty();

        foreach (var type in boundaryTypes)
        {
            type.IsSealed.Should().BeTrue($"API boundary class '{type.FullName}' must be sealed.");

            var parameters = type
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(c => c.GetParameters())
                .Concat(type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetParameters()));

            foreach (var parameter in parameters)
            {
                var paramType = parameter.ParameterType;
                var isConcreteUseCase = paramType.IsClass && paramType.Name.EndsWith("UseCase", StringComparison.OrdinalIgnoreCase);

                isConcreteUseCase.Should().BeFalse(
                    $"'{type.Name}' parameter '{parameter.Name}' must depend on IUseCase<...> abstraction rather than concrete '{paramType.Name}'.");
            }
        }
    }

    [Fact]
    public void Core_And_Infrastructure_Assemblies_Must_Not_Call_Ambient_System_UtcNow()
    {
        var assemblies = new[]
        {
            typeof(BaseEntity).Assembly,
            typeof(ICurrentProjectContext).Assembly,
            typeof(AppDbContext).Assembly
        };

        var violations = new List<string>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                var methods = type
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Cast<MethodBase>()
                    .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));

                foreach (var method in methods)
                {
                    foreach (var calledMethod in EnumerateCalledMethods(type, method))
                    {
                        var declaringType = calledMethod.DeclaringType;
                        if ((declaringType == typeof(DateTime) || declaringType == typeof(DateTimeOffset)) &&
                            calledMethod.Name is "get_UtcNow" or "get_Now")
                        {
                            violations.Add($"{type.FullName}.{method.Name} -> {declaringType.Name}.{calledMethod.Name}");
                        }
                        else if (declaringType == typeof(TimeProvider) &&
                                 calledMethod.Name == "get_System" &&
                                 type.FullName != "SmkDoc.Infrastructure.DependencyInjection")
                        {
                            violations.Add($"{type.FullName}.{method.Name} -> TimeProvider.System");
                        }
                    }
                }
            }
        }

        violations.Should().BeEmpty(
            $"Domain, Application, and Infrastructure must obtain timestamps exclusively from injected TimeProvider. Violations: {string.Join("; ", violations)}");
    }

    [Fact]
    public void Retired_Phase_P1_Types_Must_Not_Exist()
    {
        var appAssembly = typeof(ICurrentProjectContext).Assembly;
        var apiAssembly = typeof(SmkDoc.Api.Controllers.Rendering.DocumentGenerationController).Assembly;

        apiAssembly.GetType("SmkDoc.Api.Controllers.Rendering.DocumentController")
            .Should().BeNull("Monolithic DocumentController was decomposed into DocumentGenerationController, DocumentArtifactController, and DocumentHistoryController.");
        apiAssembly.GetType("SmkDoc.Api.Controllers.Authoring.TemplateController")
            .Should().BeNull("Monolithic TemplateController was decomposed into TemplateLifecycleController and TemplateAssetController.");
        apiAssembly.GetType("SmkDoc.Api.Filters.ValidateCommandFilter")
            .Should().BeNull("ValidateCommandFilter was retired in favor of ValidatingUseCaseDecorator.");
        appAssembly.GetType("SmkDoc.Application.Common.Interfaces.IJsonDataParser")
            .Should().BeNull("Fat IJsonDataParser interface was segregated into IJsonHierarchyParser, IJsonTableParser, and IJsonNamedArrayParser.");
        appAssembly.GetType("SmkDoc.Application.Modules.Authoring.Templates.Queries.PreviewTemplateHtml.PreviewTemplateHtmlUseCase")
            .Should().BeNull("Dead PreviewTemplateHtml slice was retired in Phase P1.");
        appAssembly.GetType("SmkDoc.Application.Modules.Rendering.Documents.Commands.HtmlToPdf.HtmlToPdfUseCase")
            .Should().BeNull("Dead HtmlToPdf slice was retired in Phase P1.");
        appAssembly.GetType("SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft.CommitDraftCommand")
            .Should().BeNull("2-level nested CommitDraftCommand was flattened into CommitTemplateDraftCommand.");
    }

    [Fact]
    public void Streaming_Ports_Must_Not_Expose_ByteArray_Return_Types()
    {
        var streamingPorts = new[]
        {
            typeof(IRenderEngine),
            typeof(IPdfRenderer),
            typeof(IStorageService)
        };

        var violations = new List<string>();

        foreach (var port in streamingPorts)
        {
            foreach (var method in port.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var returnType = method.ReturnType;
                bool returnsByteArray =
                    returnType == typeof(byte[]) ||
                    (returnType.IsGenericType &&
                     (returnType.GetGenericTypeDefinition() == typeof(Task<>) ||
                      returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)) &&
                     returnType.GetGenericArguments()[0] == typeof(byte[]));

                if (returnsByteArray)
                {
                    violations.Add($"{port.Name}.{method.Name} returns {returnType.Name}");
                }
            }
        }

        violations.Should().BeEmpty(
            $"streaming ports must return Stream / Task<Stream> instead of buffering byte[] on the heap ([TRAP-LOH-001]). Violations: {string.Join(", ", violations)}");
    }

    [Fact]
    public void GenerationLogRepository_Must_Not_Expose_Paged_List_Projections()
    {
        var methods = typeof(SmkDoc.Domain.Interfaces.IGenerationLogRepository)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .ToList();

        methods.Should().NotContain(
            "ListPagedAsync",
            "paged generation log read queries belong on IGenerationLogQueryService so JSONB InputData columns are never hydrated on list hot paths ([TRAP-SQL-013]).");
    }

    private static readonly OpCode[] SingleByteOpCodes = BuildSingleByteOpCodes();
    private static readonly OpCode[] TwoByteOpCodes = BuildTwoByteOpCodes();

    private static OpCode[] BuildSingleByteOpCodes()
    {
        var table = new OpCode[256];
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is OpCode op && (ushort)op.Value < 0x100)
            {
                table[(byte)op.Value] = op;
            }
        }
        return table;
    }

    private static OpCode[] BuildTwoByteOpCodes()
    {
        var table = new OpCode[256];
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is OpCode op && ((ushort)op.Value >> 8) == 0xFE)
            {
                table[(byte)(op.Value & 0xFF)] = op;
            }
        }
        return table;
    }

    private static IEnumerable<MethodBase> EnumerateCalledMethods(Type declaringType, MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null || il.Length == 0)
        {
            yield break;
        }

        var typeArgs = declaringType.IsGenericType ? declaringType.GetGenericArguments() : Type.EmptyTypes;
        var methodArgs = method is MethodInfo { IsGenericMethod: true } mi ? mi.GetGenericArguments() : Type.EmptyTypes;
        var module = declaringType.Module;

        int index = 0;
        while (index < il.Length)
        {
            OpCode op;
            byte code = il[index++];
            if (code == 0xFE)
            {
                if (index >= il.Length)
                {
                    yield break;
                }
                op = TwoByteOpCodes[il[index++]];
            }
            else
            {
                op = SingleByteOpCodes[code];
            }

            switch (op.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    index += 1;
                    break;
                case OperandType.InlineVar:
                    index += 2;
                    break;
                case OperandType.InlineMethod:
                    if (index + 4 <= il.Length)
                    {
                        int metadataToken = BitConverter.ToInt32(il, index);
                        MethodBase? resolved = null;
                        try
                        {
                            resolved = module.ResolveMethod(metadataToken, typeArgs, methodArgs);
                        }
                        catch
                        {
                            // Ignore dynamic/unresolved tokens
                        }

                        if (resolved is not null)
                        {
                            yield return resolved;
                        }
                    }
                    index += 4;
                    break;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    index += 4;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    index += 8;
                    break;
                case OperandType.InlineSwitch:
                    if (index + 4 <= il.Length)
                    {
                        int count = BitConverter.ToInt32(il, index);
                        index += 4 + (count * 4);
                    }
                    else
                    {
                        yield break;
                    }
                    break;
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
