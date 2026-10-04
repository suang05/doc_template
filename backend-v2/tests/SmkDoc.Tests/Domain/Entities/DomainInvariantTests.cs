using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class DomainInvariantTests
{
    // ── Template Invariants ──────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Template_Constructor_WithEmptyName_ThrowsDomainValidationException(string invalidName)
    {
        var act = () => new Template(Guid.NewGuid(), invalidName, "valid-slug");
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*name cannot be empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Template_Constructor_WithEmptySlug_ThrowsDomainValidationException(string invalidSlug)
    {
        var act = () => new Template(Guid.NewGuid(), "Valid Name", invalidSlug);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*slug cannot be empty*");
    }

    [Fact]
    public void Template_SetCurrentVersion_WhenInactive_ThrowsBusinessRuleViolationException()
    {
        var template = new Template(Guid.NewGuid(), "Invoice", "invoice");
        template.Deactivate();

        var act = () => template.SetCurrentVersion(Guid.NewGuid());
        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("INACTIVE_TEMPLATE");
    }

    [Fact]
    public void Template_SetCurrentVersion_WhenEmptyGuid_ThrowsDomainValidationException()
    {
        var template = new Template(Guid.NewGuid(), "Invoice", "invoice");

        var act = () => template.SetCurrentVersion(Guid.Empty);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*VersionId cannot be empty*");
    }

    // ── TemplateVersion Invariants ───────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void TemplateVersion_Constructor_WithInvalidVersionNumber_ThrowsDomainValidationException(int invalidVersion)
    {
        var act = () => new TemplateVersion(Guid.NewGuid(), invalidVersion, "key", TemplateFormat.Html, "user");
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*version number must be greater than zero*");
    }

    [Fact]
    public void TemplateVersion_Publish_WhenArchived_ThrowsBusinessRuleViolationException()
    {
        var version = new TemplateVersion(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "user");
        version.Archive();

        var act = () => version.Publish();
        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("ARCHIVED_VERSION_CANNOT_BE_PUBLISHED");
    }

    [Fact]
    public void TemplateVersion_UpdateDataSchema_WhenArchived_ThrowsBusinessRuleViolationException()
    {
        var version = new TemplateVersion(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "user");
        version.Archive();

        var act = () => version.UpdateDataSchema("{}", "{}");
        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("ARCHIVED_VERSION_CANNOT_BE_MODIFIED");
    }

    // ── UserProjectRole Invariants ───────────────────────────────────────────

    [Fact]
    public void UserProjectRole_Constructor_WithEmptyUserId_ThrowsDomainValidationException()
    {
        var act = () => new UserProjectRole(Guid.Empty, Guid.NewGuid(), RoleType.Viewer);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void UserProjectRole_Constructor_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var act = () => new UserProjectRole(Guid.NewGuid(), Guid.Empty, RoleType.Viewer);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    // ── ProjectId Invariant Hardening ────────────────────────────────────────

    [Fact]
    public void Template_Constructor_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var act = () => new Template(Guid.Empty, "Invoice", "invoice");
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Fact]
    public void ApiKey_Constructor_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var act = () => new ApiKey(Guid.Empty, "Key", "app", "hash", null);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    // ── DataConnection Invariant Hardening ───────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DataConnection_Constructor_WithEmptyConnectionString_ThrowsDomainValidationException(string invalidConn)
    {
        var act = () => new DataConnection("DB", "PostgreSQL", invalidConn);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*Encrypted connection string cannot be empty*");
    }

    // ── User Invariant Hardening ─────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void User_Constructor_WithEmptyFirstName_ThrowsDomainValidationException(string invalidFirstName)
    {
        var act = () => new User("user@example.com", "hash", invalidFirstName, "Last");
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*FirstName cannot be empty*");
    }
}
