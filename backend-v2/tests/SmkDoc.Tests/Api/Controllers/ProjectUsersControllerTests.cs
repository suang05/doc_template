using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.IdentityAccess.Users;
using SmkDoc.Api.Controllers.IdentityAccess;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.GetProjectUserById;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class ProjectUsersControllerTests
{
    private readonly IUseCase<ListProjectUsersQuery, List<UserResultDto>> _listUseCase =
        Substitute.For<IUseCase<ListProjectUsersQuery, List<UserResultDto>>>();
    private readonly IUseCase<GetProjectUserByIdQuery, UserResultDto> _getByIdUseCase =
        Substitute.For<IUseCase<GetProjectUserByIdQuery, UserResultDto>>();
    private readonly IUseCase<InviteUserCommand, UserResultDto> _inviteUseCase =
        Substitute.For<IUseCase<InviteUserCommand, UserResultDto>>();
    private readonly IUseCase<UpdateUserRoleCommand, UserResultDto> _updateRoleUseCase =
        Substitute.For<IUseCase<UpdateUserRoleCommand, UserResultDto>>();
    private readonly IUseCase<RemoveUserCommand> _removeUseCase =
        Substitute.For<IUseCase<RemoveUserCommand>>();

    private readonly Guid _currentUserId = Guid.NewGuid();

    private ProjectUsersController CreateSut()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, _currentUserId.ToString())],
            "TestAuth"));

        return new ProjectUsersController(
            _listUseCase,
            _getByIdUseCase,
            _inviteUseCase,
            _updateRoleUseCase,
            _removeUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    [Fact]
    public async Task GetById_InvokesUseCase_ReturnsOkWithUserAndVersion()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = new UserResultDto(userId, "member@smkdoc.com", "Somchai", "Jaidee", "Developer", true, DateTimeOffset.UtcNow, Version: 4);
        _getByIdUseCase.ExecuteAsync(Arg.Is<GetProjectUserByIdQuery>(q => q.UserId == userId), Arg.Any<CancellationToken>())
            .Returns(dto);

        // Act
        var result = await CreateSut().GetById(userId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = okResult.Value.Should().BeOfType<ApiResponse<UserResultDto>>().Subject;
        envelope.Data.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task List_InvokesUseCase_ReturnsOkWithUsers()
    {
        // Arrange
        var users = new List<UserResultDto>
        {
            new(Guid.NewGuid(), "dev@sammakorn.co.th", "Dev", "User", "Developer", true, DateTimeOffset.UtcNow, 1L)
        };
        _listUseCase.ExecuteAsync(Arg.Any<ListProjectUsersQuery>(), Arg.Any<CancellationToken>())
            .Returns(users);

        // Act
        var result = await CreateSut().List(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<UserResultDto>>>().Subject;
        response.Data.Should().BeEquivalentTo(users);
    }

    [Fact]
    public async Task Invite_InvokesUseCase_ReturnsCreatedWithUser()
    {
        // Arrange
        var request = new InviteUserRequest(
            "newuser@sammakorn.co.th",
            "Password123!",
            "New",
            "User",
            "Developer");
        var createdUser = new UserResultDto(
            Guid.NewGuid(),
            request.Email,
            request.FirstName,
            request.LastName,
            request.Role,
            true,
            DateTimeOffset.UtcNow,
            1L);

        _inviteUseCase.ExecuteAsync(Arg.Is<InviteUserCommand>(c => c.Email == request.Email && c.Role == request.Role), Arg.Any<CancellationToken>())
            .Returns(createdUser);

        // Act
        var result = await CreateSut().Invite(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeOfType<ApiResponse<UserResultDto>>().Subject;
        response.Data.Should().Be(createdUser);
    }

    [Fact]
    public async Task UpdateRole_InvokesUseCase_ReturnsOkWithUser()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var request = new UpdateUserRoleRequest("Admin");
        var updatedUser = new UserResultDto(
            targetUserId,
            "user@sammakorn.co.th",
            "User",
            "Test",
            "Admin",
            true,
            DateTimeOffset.UtcNow,
            2L);

        _updateRoleUseCase.ExecuteAsync(Arg.Is<UpdateUserRoleCommand>(c => c.UserId == targetUserId && c.Role == "Admin" && c.ExpectedVersion == 1L), Arg.Any<CancellationToken>())
            .Returns(updatedUser);

        // Act
        var result = await CreateSut().UpdateRole(targetUserId, 1L, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<UserResultDto>>().Subject;
        response.Data.Should().Be(updatedUser);
    }

    [Fact]
    public async Task Remove_InvokesUseCase_ReturnsNoContent()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();

        // Act
        var result = await CreateSut().Remove(targetUserId, 1L, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        await _removeUseCase.Received(1).ExecuteAsync(
            Arg.Is<RemoveUserCommand>(c => c.UserId == targetUserId && c.CurrentUserId == _currentUserId && c.ExpectedVersion == 1L),
            Arg.Any<CancellationToken>());
    }
}
