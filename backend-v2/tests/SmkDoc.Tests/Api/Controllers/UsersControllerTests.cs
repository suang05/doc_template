using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.IdentityAccess.Users;
using SmkDoc.Api.Controllers.IdentityAccess;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class UsersControllerTests
{
    private readonly IUseCase<ListProjectUsersQuery, List<UserResultDto>> _listUseCase =
        Substitute.For<IUseCase<ListProjectUsersQuery, List<UserResultDto>>>();
    private readonly IUseCase<InviteUserCommand, UserResultDto> _inviteUseCase =
        Substitute.For<IUseCase<InviteUserCommand, UserResultDto>>();
    private readonly IUseCase<UpdateUserRoleCommand, UserResultDto> _updateRoleUseCase =
        Substitute.For<IUseCase<UpdateUserRoleCommand, UserResultDto>>();
    private readonly IUseCase<RemoveUserCommand> _removeUseCase =
        Substitute.For<IUseCase<RemoveUserCommand>>();
    private readonly IUseCase<SetUserStatusCommand, UserResultDto> _setStatusUseCase =
        Substitute.For<IUseCase<SetUserStatusCommand, UserResultDto>>();

    private readonly Guid _currentUserId = Guid.NewGuid();

    private UsersController CreateSut()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, _currentUserId.ToString())],
            "TestAuth"));

        return new UsersController(
            _listUseCase,
            _inviteUseCase,
            _updateRoleUseCase,
            _removeUseCase,
            _setStatusUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    [Fact]
    public async Task List_InvokesUseCase_ReturnsOkWithUsers()
    {
        // Arrange
        var users = new List<UserResultDto>
        {
            new(Guid.NewGuid(), "dev@sammakorn.co.th", "Dev", "User", "Developer", true, DateTimeOffset.UtcNow, "54321")
        };
        _listUseCase.ExecuteAsync(Arg.Any<ListProjectUsersQuery>(), Arg.Any<CancellationToken>())
            .Returns(users);

        var controller = CreateSut();

        // Act
        var result = await controller.List(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<UserResultDto>>>().Subject;
        response.Data.Should().HaveCount(1);
        await _listUseCase.Received(1).ExecuteAsync(Arg.Any<ListProjectUsersQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddUser_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var userDto = new UserResultDto(
            Guid.NewGuid(),
            "newuser@sammakorn.co.th",
            "New",
            "User",
            "Developer",
            true,
            DateTimeOffset.UtcNow,
            "10001");

        _inviteUseCase.ExecuteAsync(Arg.Any<InviteUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(userDto);

        var controller = CreateSut();
        var request = new InviteUserRequest("newuser@sammakorn.co.th", "P@ssword123", "New", "User", "Developer");

        // Act
        var result = await controller.AddUser(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.ActionName.Should().Be(nameof(UsersController.List));
        var response = createdResult.Value.Should().BeOfType<ApiResponse<UserResultDto>>().Subject;
        response.Data.Email.Should().Be("newuser@sammakorn.co.th");
    }

    [Fact]
    public async Task UpdateRole_ReturnsOk()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var updatedDto = new UserResultDto(
            targetUserId,
            "user@sammakorn.co.th",
            "Test",
            "User",
            "Admin",
            true,
            DateTimeOffset.UtcNow,
            "10002");

        _updateRoleUseCase.ExecuteAsync(Arg.Any<UpdateUserRoleCommand>(), Arg.Any<CancellationToken>())
            .Returns(updatedDto);

        var controller = CreateSut();
        var request = new UpdateUserRoleRequest("Admin");

        // Act
        var result = await controller.UpdateRole(targetUserId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<UserResultDto>>().Subject;
        response.Data.Role.Should().Be("Admin");

        await _updateRoleUseCase.Received(1).ExecuteAsync(
            Arg.Is<UpdateUserRoleCommand>(c => c.UserId == targetUserId && c.Role == "Admin"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_PassesCurrentUserId_ReturnsNoContent()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        _removeUseCase.ExecuteAsync(Arg.Any<RemoveUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = CreateSut();

        // Act
        var result = await controller.Remove(targetUserId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        await _removeUseCase.Received(1).ExecuteAsync(
            Arg.Is<RemoveUserCommand>(c => c.UserId == targetUserId && c.CurrentUserId == _currentUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetStatus_InvokesUseCase_ReturnsOk()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var userDto = new UserResultDto(
            targetUserId,
            "user@sammakorn.co.th",
            "Test",
            "User",
            "Developer",
            false,
            DateTimeOffset.UtcNow,
            "10003");

        _setStatusUseCase.ExecuteAsync(Arg.Any<SetUserStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(userDto);

        var controller = CreateSut();
        var request = new SetUserStatusRequest(false);

        // Act
        var result = await controller.SetStatus(targetUserId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<UserResultDto>>().Subject;
        response.Data.IsActive.Should().BeFalse();

        await _setStatusUseCase.Received(1).ExecuteAsync(
            Arg.Is<SetUserStatusCommand>(c => c.UserId == targetUserId && !c.IsActive),
            Arg.Any<CancellationToken>());
    }
}

