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

public sealed class ListProjectUsersUseCaseTests_Demo
{
    // 1. ประกาศ Mock Dependencies ด้วย NSubstitute และพระเอกของงาน (_sut)
    private readonly IUserProjectRoleRepository _roleRepoMock;
    private readonly IUserRepository _userRepoMock;
    private readonly IValidator<ListProjectUsersQuery> _validatorMock;
    private readonly ListProjectUsersUseCase _sut;

    public ListProjectUsersUseCaseTests_Demo()
    {
        // 2. สร้าง Instance ใหม่ทุกครั้งก่อนรันแต่ละ Test Method (Test Isolation)
        _roleRepoMock = Substitute.For<IUserProjectRoleRepository>();
        _userRepoMock = Substitute.For<IUserRepository>();
        _validatorMock = Substitute.For<IValidator<ListProjectUsersQuery>>();

        // ฉีด Mock เข้าไปใน Use Case จริง
        _sut = CreateSut();
    }

    // SSoT Factory Method สำหรับประกอบ UseCase
    private ListProjectUsersUseCase CreateSut() =>
        new(_roleRepoMock, _userRepoMock, _validatorMock);

    // =========================================================================
    // เคสที่ 1: Happy Path — ข้อมูลถูกต้อง ต้องดึงและคืนค่ารายชื่อผู้ใช้สำเร็จ
    // =========================================================================
    [Fact]
    public async Task ExecuteAsync_ShouldReturnUsersList_WhenQueryIsValid()
    {
        // Arrange — เตรียมข้อมูลจำลอง
        var projectId = Guid.NewGuid();
        var query = new ListProjectUsersQuery(projectId);

        var user = new UserBuilder()
            .WithEmail("admin@smkdoc.com")
            .WithName("Somchai", "Jaidee")
            .Build();

        var role = new UserProjectRoleBuilder()
            .ForUser(user.Id)
            .InProject(projectId)
            .AsAdmin()
            .Build();

        // จัดฉาก Mock: ให้ Validator ตอบว่า "ผ่าน" (ValidationResult ว่างเปล่าแปลว่า IsValid = true)
        _validatorMock
            .ValidateAsync(query, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        // จัดฉาก Mock: ให้ RoleRepo คืนค่า Role ในโปรเจกต์นี้
        _roleRepoMock
            .ListByProjectAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new List<UserProjectRole> { role });

        // จัดฉาก Mock: ให้ UserRepo คืนค่า User ตาม ID ที่ผูกกับ Role
        _userRepoMock
            .GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<User> { user });

        // Act — เรียกใช้งานจริงผ่าน SUT
        var result = await _sut.ExecuteAsync(query, CancellationToken.None);

        // Assert — ตรวจสอบว่าผลลัพธ์ตรงกับที่คาดหวัง
        result.Should().ContainSingle();
        result[0].Email.Should().Be("admin@smkdoc.com");
        result[0].FirstName.Should().Be("Somchai");
        result[0].LastName.Should().Be("Jaidee");
        result[0].Role.Should().Be("Admin");

        // ตรวจสอบว่ามีการเรียก Repositories ด้วย parameter ที่ถูกต้องจริงอย่างละ 1 ครั้ง
        await _roleRepoMock.Received(1).ListByProjectAsync(projectId, Arg.Any<CancellationToken>());
        await _userRepoMock.Received(1).GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // เคสที่ 2: Error Path (Fail-Fast) — ข้อมูลไม่ผ่าน Validator ต้องโยน Exception และห้ามแตะ DB!
    // =========================================================================
    [Fact]
    public async Task ExecuteAsync_ShouldThrowValidationExceptionAndNotCallRepositories_WhenQueryIsInvalid()
    {
        // Arrange — เตรียม Query ที่ผิดพลาด (เช่น ส่ง Guid.Empty)
        var invalidQuery = new ListProjectUsersQuery(Guid.Empty);

        var validationFailures = new List<ValidationFailure>
        {
            new(nameof(ListProjectUsersQuery.ProjectId), "ProjectId cannot be empty.")
        };

        // จัดฉาก Mock: ให้ Validator ตอบว่า "ไม่ผ่าน" พร้อมรายการ Error
        _validatorMock
            .ValidateAsync(invalidQuery, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(validationFailures));

        // Act — เก็บการทำงาน Async ไว้ใน Func<Task>
        Func<Task> act = async () => await _sut.ExecuteAsync(invalidQuery, CancellationToken.None);

        // Assert 1 — ต้องโยน ValidationException ออกมาตาม Clean Architecture
        await act.Should().ThrowAsync<ValidationException>();

        // Assert 2 (สำคัญมาก!) — พิสูจน์หลัก Fail-Fast: ต้องไม่มีการเรียก Database (ทั้ง RoleRepo และ UserRepo) เลยแม้แต่ครั้งเดียว!
        await _roleRepoMock
            .DidNotReceive()
            .ListByProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        await _userRepoMock
            .DidNotReceive()
            .GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
    }
}