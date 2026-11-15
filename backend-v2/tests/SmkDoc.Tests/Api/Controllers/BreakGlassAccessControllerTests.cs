using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Controllers.TenantManagement;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Commands.GrantBreakGlassAccess;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Commands.RevokeBreakGlassAccess;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.DTOs;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Queries.GetBreakGlassGrantById;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Queries.ListBreakGlassGrants;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class BreakGlassAccessControllerTests
{
    private readonly IUseCase<ListBreakGlassGrantsQuery, IReadOnlyList<BreakGlassGrantResultDto>> _listUseCase =
        Substitute.For<IUseCase<ListBreakGlassGrantsQuery, IReadOnlyList<BreakGlassGrantResultDto>>>();
    private readonly IUseCase<GetBreakGlassGrantByIdQuery, BreakGlassGrantResultDto> _getByIdUseCase =
        Substitute.For<IUseCase<GetBreakGlassGrantByIdQuery, BreakGlassGrantResultDto>>();
    private readonly IUseCase<GrantBreakGlassAccessCommand, BreakGlassGrantResultDto> _grantUseCase =
        Substitute.For<IUseCase<GrantBreakGlassAccessCommand, BreakGlassGrantResultDto>>();
    private readonly IUseCase<RevokeBreakGlassAccessCommand> _revokeUseCase =
        Substitute.For<IUseCase<RevokeBreakGlassAccessCommand>>();

    private BreakGlassAccessController CreateSut() =>
        new(_listUseCase, _getByIdUseCase, _grantUseCase, _revokeUseCase);

    [Fact]
    public async Task GetById_ShouldInvokeUseCaseAndReturnApiResponseWithVersion()
    {
        // Arrange
        var grantId = Guid.NewGuid();
        var dto = new BreakGlassGrantResultDto(
            grantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Support troubleshooting",
            TestConstants.BaselineTime,
            TestConstants.BaselineTime.AddHours(1),
            null,
            true,
            Version: 3);

        _getByIdUseCase.ExecuteAsync(Arg.Is<GetBreakGlassGrantByIdQuery>(q => q.GrantId == grantId), Arg.Any<CancellationToken>())
            .Returns(dto);

        // Act
        IActionResult result = await CreateSut().GetById(grantId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = okResult.Value.Should().BeOfType<ApiResponse<BreakGlassGrantResultDto>>().Subject;
        envelope.Data.Should().BeEquivalentTo(dto);
    }
}
