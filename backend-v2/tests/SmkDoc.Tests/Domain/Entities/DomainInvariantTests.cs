using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
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
        var act = () => TemplateName.Create(invalidName);
        act.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Template_Constructor_WithEmptySlug_ThrowsDomainValidationException(string invalidSlug)
    {
        var act = () => TemplateSlug.Create(invalidSlug);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Template_SetCurrentVersion_WhenInactive_ThrowsBusinessRuleViolationException()
    {
        var now = DateTimeOffset.UtcNow;
        var template = Template.Create(Guid.NewGuid(), TemplateName.Create("Invoice"), TemplateSlug.Create("invoice"), null, now);
        template.Deactivate(now);

        var act = () => template.SetCurrentVersion(Guid.NewGuid(), now);
        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("INACTIVE_TEMPLATE");
    }

    [Fact]
    public void Template_SetCurrentVersion_WhenEmptyGuid_ThrowsDomainValidationException()
    {
        var now = DateTimeOffset.UtcNow;
        var template = Template.Create(Guid.NewGuid(), TemplateName.Create("Invoice"), TemplateSlug.Create("invoice"), null, now);

        var act = () => template.SetCurrentVersion(Guid.Empty, now);
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
        var act = () => TemplateVersion.Draft(Guid.NewGuid(), invalidVersion, "key", TemplateFormat.Html, "user", DateTimeOffset.UtcNow);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public void TemplateVersion_Publish_WhenArchived_ThrowsBusinessRuleViolationException()
    {
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "user", now);
        version.Archive(now);

        var act = () => version.Publish(now);
        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    [Fact]
    public void TemplateVersion_UpdateDataSchema_WhenArchived_ThrowsBusinessRuleViolationException()
    {
        var now = DateTimeOffset.UtcNow;
        var version = TemplateVersion.Draft(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "user", now);
        version.Archive(now);

        var act = () => version.UpdateDataSchema("{}", "{}", now);
        act.Should().Throw<BusinessRuleViolationException>()
            .Which.ErrorCode.Should().Be("ARCHIVED_VERSION_IMMUTABLE");
    }

    // ── UserProjectRole Invariants ───────────────────────────────────────────

    [Fact]
    public void UserProjectRole_Constructor_WithEmptyUserId_ThrowsDomainValidationException()
    {
        var act = () => UserProjectRole.Create(Guid.Empty, Guid.NewGuid(), RoleType.Viewer, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void UserProjectRole_Constructor_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var act = () => UserProjectRole.Create(Guid.NewGuid(), Guid.Empty, RoleType.Viewer, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    // ── ProjectId Invariant Hardening ────────────────────────────────────────

    [Fact]
    public void Template_Constructor_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var act = () => Template.Create(Guid.Empty, TemplateName.Create("Invoice"), TemplateSlug.Create("invoice"), null, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Fact]
    public void ApiKey_Constructor_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var act = () => ApiKey.Issue(Guid.Empty, ApiKeyName.Create("Key"), "app", new Sha256Hash(new string('a', 64)), ExpirationPolicy.Never, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    // ── DataConnection Invariant Hardening ───────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DataConnection_Constructor_WithEmptyConnectionString_ThrowsDomainValidationException(string invalidConn)
    {
        var act = () => DataConnection.Create(ConnectionName.Create("DB"), DatabaseProvider.PostgreSQL, invalidConn, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*EncryptedConnectionString cannot be empty*");
    }

    // ── User Invariant Hardening ─────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void User_Constructor_WithEmptyFirstName_ThrowsDomainValidationException(string invalidFirstName)
    {
        var act = () => User.Register(EmailAddress.Create("user@example.com"), "hash", invalidFirstName, "Last", DateTimeOffset.UtcNow);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*FirstName cannot be empty*");
    }
}
