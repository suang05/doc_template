using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

    private static (DefaultHttpContext context, CurrentProjectContext projectContext, ExecutionContextImpl executionContext) CreateContexts(string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Response.Body = new MemoryStream();

        var projectContext = new CurrentProjectContext();
        var executionContext = new ExecutionContextImpl();

        return (httpContext, projectContext, executionContext);
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
    [InlineData("/api/documents/preview")]
    public async Task InvokeAsync_WhitelistedPath_BypassesResolutionAndCallsNext(string path)
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext, executionContext) = CreateContexts(path);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo,
            executionContext);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_ValidApiKey_ResolvesProjectAndCallsNext()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext, executionContext) = CreateContexts("/api/v1/templates");

        string rawKey = "valid-raw-key";
        httpContext.Request.Headers["X-API-Key"] = rawKey;

        var targetProjectId = ProjectId.New();
        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(rawKey));

        var apiKey = (ApiKey)Activator.CreateInstance(typeof(ApiKey), nonPublic: true)!;
        typeof(ApiKey).GetProperty(nameof(ApiKey.ProjectId))!.SetValue(apiKey, targetProjectId);
        typeof(ApiKey).GetProperty(nameof(ApiKey.CallerApp))!.SetValue(apiKey, "crm-system");
        typeof(ApiKey).GetProperty(nameof(ApiKey.Scope))!.SetValue(apiKey, ApiKeyScope.ReadWrite);
        typeof(ApiKey).GetProperty(nameof(ApiKey.IsActive))!.SetValue(apiKey, true);
        typeof(ApiKey).GetProperty(nameof(ApiKey.Expiration))!.SetValue(apiKey, ExpirationPolicy.Never);
        typeof(ApiKey).GetProperty(nameof(ApiKey.KeyHash))!.SetValue(apiKey, hash);

        _apiKeyRepo.GetByKeyHashAsync(hash, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo,
            executionContext);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        executionContext.ProjectId.Should().Be(targetProjectId.Value);
        executionContext.CallerApp.Should().Be("crm-system");
        executionContext.Scope.Should().Be(ApiKeyScope.ReadWrite);
    }

    [Fact]
    public async Task InvokeAsync_InvalidApiKey_Returns401Unauthorized()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext, executionContext) = CreateContexts("/api/v1/templates");

        httpContext.Request.Headers["X-API-Key"] = "invalid-key";

        _apiKeyRepo.GetByKeyHashAsync(Arg.Any<Sha256Hash>(), Arg.Any<CancellationToken>())
            .Returns((ApiKey?)null);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo,
            executionContext);

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
        var (httpContext, projectContext, executionContext) = CreateContexts("/api/v1/templates");

        var targetProjectId = ProjectId.New();
        httpContext.Request.Headers["X-API-Key"] = "master-secret-key";
        httpContext.Request.Headers["X-Project-Id"] = targetProjectId.ToString();

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo,
            executionContext);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        executionContext.CallerApp.Should().Be("master");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedPortalUser_WithValidMembership_ResolvesProjectAndCallsNext()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext, executionContext) = CreateContexts("/api/v1/templates");

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
            _projectRepo,
            executionContext);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        executionContext.UserId.Should().Be(userId);
        executionContext.ProjectId.Should().Be(targetProjectId.Value);
        executionContext.CallerApp.Should().Be("portal");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedPortalUser_WithoutMembership_Returns403Forbidden()
    {
        // Arrange: IDOR Defense Test
        var sut = CreateSut();
        var (httpContext, projectContext, executionContext) = CreateContexts("/api/v1/templates");

        var userId = Guid.NewGuid();
        var unauthorizedProjectId = ProjectId.New();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("SystemRole", "Member")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        httpContext.Request.Headers["X-Project-Id"] = unauthorizedProjectId.ToString();

        // User does NOT hold membership in target project
        _userProjectRoleRepo.GetAsync(unauthorizedProjectId, new UserId(userId), Arg.Any<CancellationToken>())
            .Returns((UserProjectRole?)null);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo,
            executionContext);

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
        var (httpContext, projectContext, executionContext) = CreateContexts("/api/v1/templates");

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
            _projectRepo,
            executionContext);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        await _userProjectRoleRepo.DidNotReceive().GetAsync(Arg.Any<ProjectId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedPortalUser_WithoutTargetProjectId_AllowsUnresolvedContext()
    {
        // Arrange: User calling general endpoint like /api/v1/projects without project selection
        var sut = CreateSut();
        var (httpContext, projectContext, executionContext) = CreateContexts("/api/v1/projects");

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
            _projectRepo,
            executionContext);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        executionContext.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedRequest_OnSecuredPath_Returns401Unauthorized()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext, executionContext) = CreateContexts("/api/v1/templates");

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo,
            executionContext);

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
        var (httpContext, projectContext, executionContext) = CreateContexts("/api/v1/templates");

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
            _projectRepo,
            executionContext);

        // Assert
        await _next.Received(1).Invoke(httpContext);
        projectContext.IsResolved.Should().BeTrue();
        projectContext.ProjectId.Should().Be(targetProjectId);
        executionContext.CallerApp.Should().Be("portal");
        executionContext.UserId.Should().Be(userId);
        await _apiKeyRepo.DidNotReceive().GetByKeyHashAsync(Arg.Any<Sha256Hash>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ApiKeyWithMismatchedProjectIdHeader_Returns403Forbidden()
    {
        // Arrange
        var sut = CreateSut();
        var (httpContext, projectContext, executionContext) = CreateContexts("/api/v1/templates");

        string rawKey = "valid-raw-key";
        httpContext.Request.Headers["X-API-Key"] = rawKey;

        var keyProjectId = ProjectId.New();
        var spoofedProjectId = ProjectId.New();
        httpContext.Request.Headers["X-Project-Id"] = spoofedProjectId.ToString();

        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(rawKey));

        var apiKey = (ApiKey)Activator.CreateInstance(typeof(ApiKey), nonPublic: true)!;
        typeof(ApiKey).GetProperty(nameof(ApiKey.ProjectId))!.SetValue(apiKey, keyProjectId);
        typeof(ApiKey).GetProperty(nameof(ApiKey.CallerApp))!.SetValue(apiKey, "crm-system");
        typeof(ApiKey).GetProperty(nameof(ApiKey.Scope))!.SetValue(apiKey, ApiKeyScope.ReadWrite);
        typeof(ApiKey).GetProperty(nameof(ApiKey.IsActive))!.SetValue(apiKey, true);
        typeof(ApiKey).GetProperty(nameof(ApiKey.Expiration))!.SetValue(apiKey, ExpirationPolicy.Never);
        typeof(ApiKey).GetProperty(nameof(ApiKey.KeyHash))!.SetValue(apiKey, hash);

        _apiKeyRepo.GetByKeyHashAsync(hash, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        // Act
        await sut.InvokeAsync(
            httpContext,
            projectContext,
            _validateApiKeyUseCase,
            _userProjectRoleRepo,
            _projectRepo,
            executionContext);

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
        var (httpContext, projectContext, executionContext) = CreateContexts("/api/v1/templates");

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
            _projectRepo,
            executionContext);

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
        var (httpContext, projectContext, executionContext) = CreateContexts("/api/v1/templates");

        string rawKey = "deactivated-raw-key";
        httpContext.Request.Headers["X-API-Key"] = rawKey;

        var targetProjectId = ProjectId.New();
        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(rawKey));

        var apiKey = (ApiKey)Activator.CreateInstance(typeof(ApiKey), nonPublic: true)!;
        typeof(ApiKey).GetProperty(nameof(ApiKey.ProjectId))!.SetValue(apiKey, targetProjectId);
        typeof(ApiKey).GetProperty(nameof(ApiKey.CallerApp))!.SetValue(apiKey, "crm-system");
        typeof(ApiKey).GetProperty(nameof(ApiKey.Scope))!.SetValue(apiKey, ApiKeyScope.ReadWrite);
        typeof(ApiKey).GetProperty(nameof(ApiKey.IsActive))!.SetValue(apiKey, true);
        typeof(ApiKey).GetProperty(nameof(ApiKey.Expiration))!.SetValue(apiKey, ExpirationPolicy.Never);
        typeof(ApiKey).GetProperty(nameof(ApiKey.KeyHash))!.SetValue(apiKey, hash);

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
            _projectRepo,
            executionContext);

        // Assert
        await _next.DidNotReceive().Invoke(httpContext);
        projectContext.IsResolved.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");
    }
}
