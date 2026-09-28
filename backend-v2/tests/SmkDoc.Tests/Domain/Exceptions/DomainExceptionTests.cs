using FluentAssertions;
using SmkDoc.Domain.Exceptions;
using Xunit;

namespace SmkDoc.Tests.Domain.Exceptions;

public class DomainExceptionTests
{
    [Fact]
    public void NotFoundException_ShouldHaveCorrectDefaults()
    {
        var ex = new NotFoundException("Template", "tpl-123");
        ex.ErrorCode.Should().Be("RESOURCE_NOT_FOUND");
        ex.Message.Should().Contain("tpl-123");
    }

    [Fact]
    public void DraftExpiredException_ShouldHaveCorrectDefaults()
    {
        var ex = new DraftExpiredException("draft-abc");
        ex.ErrorCode.Should().Be("DRAFT_EXPIRED");
        ex.DraftId.Should().Be("draft-abc");
    }

    [Fact]
    public void ConflictException_ShouldHaveCorrectDefaults()
    {
        var ex = ConflictException.DuplicateSlug("invoice-slug");
        ex.ErrorCode.Should().Be("RESOURCE_CONFLICT");
        ex.Message.Should().Contain("invoice-slug");
    }

    [Fact]
    public void RenderException_ShouldHaveCorrectDefaults()
    {
        var ex = new RenderException("sales-report", "Chromium", "Process killed");
        ex.ErrorCode.Should().Be("DOCUMENT_RENDER_FAILED");
        ex.TemplateSlug.Should().Be("sales-report");
        ex.EngineType.Should().Be("Chromium");
    }

    [Fact]
    public void SchemaValidationException_ShouldHaveCorrectDefaults()
    {
        var errors = new List<SchemaValidationError>
        {
            new("/customer/name", "Customer name is required", "required")
        };
        var ex = new SchemaValidationException("invoice", 2, errors);
        ex.ErrorCode.Should().Be("SCHEMA_VALIDATION_FAILED");
        ex.TemplateSlug.Should().Be("invoice");
        ex.Version.Should().Be(2);
        ex.Errors.Should().HaveCount(1);
    }
}
