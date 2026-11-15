using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.IdentityAccess.Users;
using SmkDoc.Api.Controllers.IdentityAccess;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.GetUserById;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class UserManagementControllerTests
{
    private readonly IUseCase<GetUserByIdQuery, UserResultDto> _getByIdUseCase =
        Substitute.For<IUseCase<GetUserByIdQuery, UserResultDto>>();
    private readonly IUseCase<SetUserStatusCommand, UserResultDto> _setStatusUseCase =
        Substitute.For<IUseCase<SetUserStatusCommand, UserResultDto>>();

    private UserManagementController CreateSut() =>
        new(_getByIdUseCase, _setStatusUseCase);

    [Fact]
    public async Task GetById_InvokesUseCase_ReturnsOkWithUserAndVersion()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = new UserResultDto(userId, "admin@sammakorn.co.th", "Global", "User", "Member", true, DateTimeOffset.UtcNow, Version: 5);
        _getByIdUseCase.ExecuteAsync(Arg.Is<GetUserByIdQuery>(q => q.UserId == userId), Arg.Any<CancellationToken>())
            .Returns(dto);

        // Act
        var result = await CreateSut().GetById(userId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = okResult.Value.Should().BeOfType<ApiResponse<UserResultDto>>().Subject;
        envelope.Data.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task SetStatus_InvokesUseCase_ReturnsOkWithUser()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var request = new SetUserStatusRequest(false);
        var updatedUser = new UserResultDto(
            targetUserId,
            "user@sammakorn.co.th",
            "User",
            "Test",
            "Developer",
            false,
            DateTimeOffset.UtcNow,
            2L);

        _setStatusUseCase.ExecuteAsync(Arg.Is<SetUserStatusCommand>(c => c.UserId == targetUserId && !c.IsActive && c.ExpectedVersion == 1L), Arg.Any<CancellationToken>())
            .Returns(updatedUser);

        // Act
        var result = await CreateSut().SetStatus(targetUserId, 1L, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<UserResultDto>>().Subject;
        response.Data.Should().Be(updatedUser);
    }
}
