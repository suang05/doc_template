using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;

public sealed class ListProjectUsersUseCaseTests
{
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IValidator<ListProjectUsersQuery> _validator = Substitute.For<IValidator<ListProjectUsersQuery>>();

    private ListProjectUsersUseCase CreateSut() => new(
        _roleRepo,
        _userRepo,
        _validator);

    [Fact]
    public async Task ExecuteAsync_WhenUsersExist_ShouldReturnProjectUserSummaries()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithEmail("admin@smkdoc.com")
            .WithName("Somchai", "Jaidee")
            .Build();

        var role = new UserProjectRoleBuilder()
            .ForUser(user.Id)
            .InProject(projectId)
            .AsAdmin()
            .Build();

        _validator.ValidateAsync(Arg.Any<ListProjectUsersQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _roleRepo.ListByProjectAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new List<UserProjectRole> { role });

        _userRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<User> { user });

        var query = new ListProjectUsersQuery(projectId);

        // Act
        var result = await CreateSut().ExecuteAsync(query);

        // Assert
        result.Should().ContainSingle();
        result[0].Email.Should().Be("admin@smkdoc.com");
        result[0].FirstName.Should().Be("Somchai");
        result[0].LastName.Should().Be("Jaidee");
        result[0].Role.Should().Be("Admin");

        await _roleRepo.Received(1).ListByProjectAsync(projectId, Arg.Any<CancellationToken>());
        await _userRepo.Received(1).GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenQueryIsInvalid_ShouldThrowValidationExceptionAndNotCallRepositories()
    {
        // Arrange
        var query = new ListProjectUsersQuery(Guid.Empty);
        _validator.ValidateAsync(query, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("ProjectId", "ProjectId is required.")]));

        // Act
        var act = () => CreateSut().ExecuteAsync(query);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _roleRepo.DidNotReceive().ListByProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _userRepo.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
    }
}
