using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.IdentityAccess.Users;
using SmkDoc.Api.Controllers.IdentityAccess;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class UserManagementControllerTests
{
    private readonly IUseCase<SetUserStatusCommand, UserResultDto> _setStatusUseCase =
        Substitute.For<IUseCase<SetUserStatusCommand, UserResultDto>>();

    private UserManagementController CreateSut() =>
        new(_setStatusUseCase);

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
            "12345");

        _setStatusUseCase.ExecuteAsync(Arg.Is<SetUserStatusCommand>(c => c.UserId == targetUserId && !c.IsActive), Arg.Any<CancellationToken>())
            .Returns(updatedUser);

        // Act
        var result = await CreateSut().SetStatus(targetUserId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<UserResultDto>>().Subject;
        response.Data.Should().Be(updatedUser);
    }
}
