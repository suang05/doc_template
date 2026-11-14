using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.IdentityAccess.Auth;
using SmkDoc.Api.Controllers.IdentityAccess;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class AuthControllerTests
{
    private readonly IUseCase<LoginCommand, LoginResultDto> _loginUseCase =
        Substitute.For<IUseCase<LoginCommand, LoginResultDto>>();
    private readonly IUseCase<RefreshTokenCommand, TokenResultDto> _refreshTokenUseCase =
        Substitute.For<IUseCase<RefreshTokenCommand, TokenResultDto>>();
    private readonly IUseCase<GetCurrentUserProfileQuery, CurrentUserProfileResultDto> _getCurrentUserProfileUseCase =
        Substitute.For<IUseCase<GetCurrentUserProfileQuery, CurrentUserProfileResultDto>>();

    private readonly Guid _currentUserId = Guid.NewGuid();

    private AuthController CreateSut()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, _currentUserId.ToString())],
            "Bearer"));

        return new AuthController(
            _loginUseCase,
            _refreshTokenUseCase,
            _getCurrentUserProfileUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithLoginResult()
    {
        // Arrange
        var loginResultDto = new LoginResultDto
        {
            AccessToken = "jwt-access-token",
            RefreshToken = "raw-refresh-token",
            User = new UserProfileResultDto(_currentUserId, "admin@sammakorn.co.th", "Super", "Admin", "SuperAdmin"),
            AccessibleProjects = [],
            DefaultProjectId = null
        };

        _loginUseCase.ExecuteAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(loginResultDto);

        var controller = CreateSut();
        var request = new LoginRequest("admin@sammakorn.co.th", "P@ssword123");

        // Act
        var result = await controller.Login(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<LoginResultDto>>().Subject;
        response.Data.AccessToken.Should().Be("jwt-access-token");
        response.Data.RefreshToken.Should().Be("raw-refresh-token");
        response.Data.User.Email.Should().Be("admin@sammakorn.co.th");

        await _loginUseCase.Received(1).ExecuteAsync(
            Arg.Is<LoginCommand>(c => c.Email == "admin@sammakorn.co.th" && c.Password == "P@ssword123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshToken_WithValidRefreshToken_ReturnsOkWithTokenPair()
    {
        // Arrange
        var tokenResultDto = new TokenResultDto(
            AccessToken: "new-jwt-access-token",
            RefreshToken: "new-refresh-token");

        _refreshTokenUseCase.ExecuteAsync(Arg.Any<RefreshTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(tokenResultDto);

        var controller = CreateSut();
        var request = new RefreshTokenRequest("existing-refresh-token");

        // Act
        var result = await controller.RefreshToken(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<TokenResultDto>>().Subject;
        response.Data.AccessToken.Should().Be("new-jwt-access-token");
        response.Data.RefreshToken.Should().Be("new-refresh-token");

        await _refreshTokenUseCase.Received(1).ExecuteAsync(
            Arg.Is<RefreshTokenCommand>(c => c.RefreshToken == "existing-refresh-token"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMe_WithAuthenticatedUser_ReturnsCurrentUserProfile()
    {
        // Arrange
        var profileDto = new CurrentUserProfileResultDto(
            Id: _currentUserId,
            Email: "user@sammakorn.co.th",
            FirstName: "Test",
            LastName: "User",
            SystemRole: "Member",
            AccessibleProjects: [],
            DefaultProjectId: null);

        _getCurrentUserProfileUseCase.ExecuteAsync(Arg.Any<GetCurrentUserProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(profileDto);

        var controller = CreateSut();

        // Act
        var result = await controller.GetMe(ct: CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<CurrentUserProfileResultDto>>().Subject;
        response.Data.Id.Should().Be(_currentUserId);
        response.Data.Email.Should().Be("user@sammakorn.co.th");

        await _getCurrentUserProfileUseCase.Received(1).ExecuteAsync(
            Arg.Is<GetCurrentUserProfileQuery>(q => q.UserId == _currentUserId && q.ActiveProjectId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMe_WithProjectIdHeader_PassesActiveProjectIdInQuery()
    {
        // Arrange
        var activeProjectId = Guid.NewGuid();
        var controller = CreateSut();

        var profileDto = new CurrentUserProfileResultDto(
            Id: _currentUserId,
            Email: "user@sammakorn.co.th",
            FirstName: "Test",
            LastName: "User",
            SystemRole: "Member",
            AccessibleProjects: [],
            DefaultProjectId: activeProjectId,
            ActiveProjectId: activeProjectId);

        _getCurrentUserProfileUseCase.ExecuteAsync(Arg.Any<GetCurrentUserProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(profileDto);

        // Act
        var result = await controller.GetMe(activeProjectId.ToString(), CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<CurrentUserProfileResultDto>>().Subject;
        response.Data.ActiveProjectId.Should().Be(activeProjectId);

        await _getCurrentUserProfileUseCase.Received(1).ExecuteAsync(
            Arg.Is<GetCurrentUserProfileQuery>(q => q.UserId == _currentUserId && q.ActiveProjectId == activeProjectId),
            Arg.Any<CancellationToken>());
    }
}

