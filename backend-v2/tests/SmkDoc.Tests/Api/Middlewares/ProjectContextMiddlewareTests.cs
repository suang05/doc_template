using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Middleware;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Tests.Common;

namespace SmkDoc.Tests.Api.Middlewares;

public sealed class ProjectContextMiddlewareTests
{
    private readonly IApiKeyRepository _apiKeyRepo = Substitute.For<IApiKeyRepository>();
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUserProjectRoleRepository _userProjectRoleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly RequestDelegate _next = Substitute.For<RequestDelegate>();
    private readonly ILogger<ProjectContextMiddleware> _logger = Substitute.For<ILogger<ProjectContextMiddleware>>();
    private readonly IConfiguration _configuration;
    private readonly ValidateApiKeyUseCase _validateApiKeyUseCase;

    public ProjectContextMiddlewareTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MASTER_API_KEY"] = "master-secret-key"
            })
            .Build();

        var defaultProject = Project.Create(
            CompanyId.New(),
            ProjectName.Create("Default Test Project"),
            ProjectSlug.Create("default-test-project"),
            DateTimeOffset.UtcNow);
        _projectRepo.GetByIdAsync(Arg.Any<ProjectId>(), Arg.Any<CancellationToken>())
            .Returns(defaultProject);

        _validateApiKeyUseCase = new ValidateApiKeyUseCase(
            _apiKeyRepo,
            _projectRepo,
            _unitOfWork,
            TimeProvider.System);

        _next.Invoke(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
    }

    private ProjectContextMiddleware CreateSut()
    {
        return new ProjectContextMiddleware(
            _next,
            _logger,
            _configuration);
    }

    private static (DefaultHttpContext context, CurrentProjectContext projectContext) CreateContexts(string path, string method = "GET")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Path = path;
        httpContext.Response.Body = new MemoryStream();

        var projectContext = new CurrentProjectContext();

        return (httpContext, projectContext);
    }

    private static ApiKey CreateHydratedApiKey(
        ProjectId projectId,
        Sha256Hash hash,
        ApiKeyScope scope,
        string callerApp = "crm-system",
        ApiKeyId? id = null)
    {
        var apiKey = (ApiKey)Activator.CreateInstance(typeof(ApiKey), nonPublic: true)!;
        typeof(ApiKey).GetProperty(nameof(ApiKey.Id))!.SetValue(apiKey, id ?? ApiKeyId.New());
        typeof(ApiKey).GetProperty(nameof(ApiKey.ProjectId))!.SetValue(apiKey, projectId);
        typeof(ApiKey).GetProperty(nameof(ApiKey.CallerApp))!.SetValue(apiKey, callerApp);
        typeof(ApiKey).GetProperty(nameof(ApiKey.Scope))!.SetValue(apiKey, scope);
        typeof(ApiKey).GetProperty(nameof(ApiKey.IsActive))!.SetValue(apiKey, true);
        typeof(ApiKey).GetProperty(nameof(ApiKey.Expiration))!.SetValue(apiKey, ExpirationPolicy.Never);
        typeof(ApiKey).GetProperty(nameof(ApiKey.KeyHash))!.SetValue(apiKey, hash);
        return apiKey;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/health")]
    [InlineData("/healthz")]
    [InlineData("/ready")]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger")]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/api/v1/auth/refresh")]
    [InlineData("/api/v1/schemas/validate")]
    public async Task InvokeAsync_WhitelistedPath_BypassesResolutionAndCallsNext(string path)
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts(path);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("/api/documents/preview")]
    [InlineData("/api/v1/documents/preview")]
    public async Task InvokeAsync_UnauthenticatedDocumentPreview_Returns401Unauthorized(string path)
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts(path, "POST");

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.DidNotReceive().Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_ValidApiKey_ResolvesProjectAndHydratesApiKeyActor()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/templates");

        string rawKey = "valid-raw-key";
        httpContext.Request.Headers["X-API-Key"] = rawKey;

        var targetProjectId = ProjectId.New();
        var apiKeyId = ApiKeyId.New();
        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(rawKey));
        var apiKey = CreateHydratedApiKey(targetProjectId, hash, ApiKeyScope.ReadWrite, "crm-system", apiKeyId);

        _apiKeyRepo.GetByKeyHashAsync(hash, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        projectContext.ApiKeyId.Should().Be(apiKeyId);
        projectContext.CallerIdentity.Should().Be("crm-system");
        projectContext.UserId.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_ReadOnlyApiKey_OnMutatingEndpoint_Returns403Forbidden()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/documents/generate/invoice", "POST");

        string rawKey = "readonly-raw-key";
        httpContext.Request.Headers["X-API-Key"] = rawKey;

        var targetProjectId = ProjectId.New();
        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(rawKey));
        var apiKey = CreateHydratedApiKey(targetProjectId, hash, ApiKeyScope.ReadOnly, "reporting-bot");

        _apiKeyRepo.GetByKeyHashAsync(hash, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.DidNotReceive().Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Theory]
    [InlineData("GET", "/api/v1/templates")]
    [InlineData("POST", "/api/v1/documents/preview")]
    [InlineData("POST", "/api/v1/templates/validate")]
    public async Task InvokeAsync_ReadOnlyApiKey_OnReadOrStatelessEndpoint_ResolvesProjectAndCallsNext(string method, string path)
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts(path, method);

        string rawKey = "readonly-raw-key";
        httpContext.Request.Headers["X-API-Key"] = rawKey;

        var targetProjectId = ProjectId.New();
        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(rawKey));
        var apiKey = CreateHydratedApiKey(targetProjectId, hash, ApiKeyScope.ReadOnly, "reporting-bot");

        _apiKeyRepo.GetByKeyHashAsync(hash, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        projectContext.CallerIdentity.Should().Be("reporting-bot");
    }

    [Fact]
    public async Task InvokeAsync_InvalidApiKey_Returns401Unauthorized()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/templates");

        httpContext.Request.Headers["X-API-Key"] = "invalid-key";

        _apiKeyRepo.GetByKeyHashAsync(Arg.Any<Sha256Hash>(), Arg.Any<CancellationToken>())
            .Returns((ApiKey?)null);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.DidNotReceive().Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_MasterApiKey_WithProjectHeader_ResolvesProjectAndCallsNext()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/templates");

        var targetProjectId = ProjectId.New();
        httpContext.Request.Headers["X-API-Key"] = "master-secret-key";
        httpContext.Request.Headers["X-Project-Id"] = targetProjectId.ToString();

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        projectContext.CallerIdentity.Should().Be("master");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedPortalUser_WithValidMembership_ResolvesProjectAndCallsNext()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/templates");

        var userId = Guid.NewGuid();
        var targetProjectId = ProjectId.New();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("SystemRole", "Member")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        httpContext.Request.Headers["X-Project-Id"] = targetProjectId.ToString();

        var roleAssignment = UserProjectRole.Create(new UserId(userId), targetProjectId, RoleType.Developer, TestConstants.BaselineTime);
        _userProjectRoleRepo.GetAsync(targetProjectId, new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(roleAssignment);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        projectContext.UserId.Should().Be(new UserId(userId));
        projectContext.SystemRole.Should().Be(SystemRole.Member);
        projectContext.CallerIdentity.Should().Be($"User:{userId:N}");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedPortalUser_WithoutMembership_Returns403Forbidden()
    {
        // Arrange: IDOR Defense Test
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/templates");

        var userId = Guid.NewGuid();
        var unauthorizedProjectId = ProjectId.New();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("SystemRole", "Member")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        httpContext.Request.Headers["X-Project-Id"] = unauthorizedProjectId.ToString();

        _userProjectRoleRepo.GetAsync(unauthorizedProjectId, new UserId(userId), Arg.Any<CancellationToken>())
            .Returns((UserProjectRole?)null);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.DidNotReceive().Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedPortalUser_WithSuperAdminRole_BypassesMembershipCheck()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/templates");

        var userId = Guid.NewGuid();
        var targetProjectId = ProjectId.New();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("SystemRole", "SuperAdmin")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        httpContext.Request.Headers["X-Project-Id"] = targetProjectId.ToString();

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        projectContext.SystemRole.Should().Be(SystemRole.SuperAdmin);
        await _userProjectRoleRepo.DidNotReceive().GetAsync(Arg.Any<ProjectId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_SuperAdminWithoutTargetProjectId_HydratesGlobalRoleClaimsOnGlobalRoute()
    {
        // Arrange: SuperAdmin calling POST /api/v1/management/projects without X-Project-Id
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/management/projects", "POST");

        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("SystemRole", "SuperAdmin")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        projectContext.UserId.Should().Be(new UserId(userId));
        projectContext.SystemRole.Should().Be(SystemRole.SuperAdmin);
        httpContext.User.IsInRole(RoleConstants.SuperAdmin).Should().BeTrue();
        httpContext.User.IsInRole(RoleConstants.Admin).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedPortalUser_WithoutTargetProjectId_AllowsUnresolvedContext()
    {
        // Arrange: User calling general endpoint like /api/v1/projects without project selection
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/projects");

        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("SystemRole", "Member")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        projectContext.UserId.Should().Be(new UserId(userId));
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedRequest_OnSecuredPath_Returns401Unauthorized()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/templates");

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.DidNotReceive().Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_DualAuthChannels_BearerTokenTakesPrecedenceOverApiKey()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/templates");

        var userId = Guid.NewGuid();
        var targetProjectId = ProjectId.New();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("SystemRole", "SuperAdmin")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        httpContext.Request.Headers["X-Project-Id"] = targetProjectId.ToString();
        httpContext.Request.Headers["X-API-Key"] = "some-m2m-api-key";

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        projectContext.CallerIdentity.Should().Be($"User:{userId:N}");
        projectContext.UserId.Should().Be(new UserId(userId));
        projectContext.ApiKeyId.Should().BeNull();
        await _apiKeyRepo.DidNotReceive().GetByKeyHashAsync(Arg.Any<Sha256Hash>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ApiKeyWithMismatchedProjectIdHeader_Returns403Forbidden()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/templates");

        string rawKey = "valid-raw-key";
        httpContext.Request.Headers["X-API-Key"] = rawKey;

        var keyProjectId = ProjectId.New();
        var spoofedProjectId = ProjectId.New();
        httpContext.Request.Headers["X-Project-Id"] = spoofedProjectId.ToString();

        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(rawKey));
        var apiKey = CreateHydratedApiKey(keyProjectId, hash, ApiKeyScope.ReadWrite, "crm-system");

        _apiKeyRepo.GetByKeyHashAsync(hash, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.DidNotReceive().Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedPortalUser_DeactivatedProject_Returns403Forbidden()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/templates");

        var userId = Guid.NewGuid();
        var deactivatedProjectId = ProjectId.New();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("SystemRole", "Member")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        httpContext.Request.Headers["X-Project-Id"] = deactivatedProjectId.ToString();

        var deactivatedProject = Project.Create(
            CompanyId.New(),
            ProjectName.Create("Deactivated Project"),
            ProjectSlug.Create("deactivated-project"),
            DateTimeOffset.UtcNow);
        deactivatedProject.Deactivate(DateTimeOffset.UtcNow);

        _projectRepo.GetByIdAsync(deactivatedProjectId, Arg.Any<CancellationToken>())
            .Returns(deactivatedProject);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.DidNotReceive().Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_ApiKey_DeactivatedProject_Returns403Forbidden()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/templates");

        string rawKey = "deactivated-raw-key";
        httpContext.Request.Headers["X-API-Key"] = rawKey;

        var targetProjectId = ProjectId.New();
        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(rawKey));
        var apiKey = CreateHydratedApiKey(targetProjectId, hash, ApiKeyScope.ReadWrite, "crm-system");

        _apiKeyRepo.GetByKeyHashAsync(hash, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        var deactivatedProject = Project.Create(
            CompanyId.New(),
            ProjectName.Create("Deactivated Project"),
            ProjectSlug.Create("deactivated-project"),
            DateTimeOffset.UtcNow);
        deactivatedProject.Deactivate(DateTimeOffset.UtcNow);

        _projectRepo.GetByIdAsync(targetProjectId, Arg.Any<CancellationToken>())
            .Returns(deactivatedProject);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.DidNotReceive().Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_PortalUser_RouteProjectIdProvided_ResolvesProjectAndHydratesRoleClaim()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/management/projects/users");

        var userId = Guid.NewGuid();
        var targetProjectId = ProjectId.New();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString())
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        httpContext.Request.RouteValues["projectId"] = targetProjectId.Value.ToString();

        var activeProject = Project.Create(
            CompanyId.New(),
            ProjectName.Create("Test Project"),
            ProjectSlug.Create("test-project"),
            DateTimeOffset.UtcNow);

        _projectRepo.GetByIdAsync(targetProjectId, Arg.Any<CancellationToken>())
            .Returns(activeProject);

        var roleMapping = UserProjectRole.Create(new UserId(userId), targetProjectId, RoleType.Admin, DateTimeOffset.UtcNow);
        _userProjectRoleRepo.GetAsync(targetProjectId, new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(roleMapping);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        projectContext.UserId.Should().Be(new UserId(userId));
        httpContext.User.IsInRole("Admin").Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_PortalUser_RouteAndHeaderMismatch_Returns400BadRequest()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/management/projects/users");

        var userId = Guid.NewGuid();
        var routeProjectId = Guid.NewGuid();
        var headerProjectId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString())
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        httpContext.Request.RouteValues["projectId"] = routeProjectId.ToString();
        httpContext.Request.Headers[HttpHeaderConstants.ProjectIdHeader] = headerProjectId.ToString();

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.DidNotReceive().Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_PortalUser_SuperAdmin_HydratesBothSuperAdminAndAdminRoles()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext) = CreateContexts("/api/v1/management/projects/users");

        var userId = Guid.NewGuid();
        var targetProjectId = ProjectId.New();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new("SystemRole", "SuperAdmin")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        httpContext.Request.RouteValues["projectId"] = targetProjectId.Value.ToString();

        var activeProject = Project.Create(
            CompanyId.New(),
            ProjectName.Create("Test Project"),
            ProjectSlug.Create("test-project"),
            DateTimeOffset.UtcNow);

        _projectRepo.GetByIdAsync(targetProjectId, Arg.Any<CancellationToken>())
            .Returns(activeProject);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        httpContext.User.IsInRole("SuperAdmin").Should().BeTrue();
        httpContext.User.IsInRole("Admin").Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_PortalUser_PathFallbackExtraction_ResolvesProjectSuccessfully()
    {
        // Arrange
        var sut = CreateSut();
        var targetProjectId = ProjectId.New();
        var (httpContext, projectContext) = CreateContexts($"/api/v1/management/projects/{targetProjectId.Value}/users");

        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString())
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        var activeProject = Project.Create(
            CompanyId.New(),
            ProjectName.Create("Test Project"),
            ProjectSlug.Create("test-project"),
            DateTimeOffset.UtcNow);

        _projectRepo.GetByIdAsync(targetProjectId, Arg.Any<CancellationToken>())
            .Returns(activeProject);

        var roleMapping = UserProjectRole.Create(new UserId(userId), targetProjectId, RoleType.Developer, DateTimeOffset.UtcNow);
        _userProjectRoleRepo.GetAsync(targetProjectId, new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(roleMapping);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        httpContext.User.IsInRole("Developer").Should().BeTrue();
    }
}
