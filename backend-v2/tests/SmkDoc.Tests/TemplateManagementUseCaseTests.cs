using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.UseCases.Templates;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using Xunit;

namespace SmkDoc.Tests;

public class TemplateManagementUseCaseTests
{
    private readonly Mock<IRepository<Template>>        _mockTemplateRepo = new();
    private readonly Mock<IRepository<TemplateVersion>> _mockVersionRepo  = new();
    private readonly Mock<IStorageService>              _mockStorage      = new();
    private readonly Mock<ITemplateScannerService>      _mockScanner      = new();
    private readonly Mock<IDocxSecurityScanner>         _mockSecurity     = new();
    private readonly Mock<IExecutionContext>             _mockContext      = new();
    private readonly Mock<IUnitOfWork>                  _mockUow          = new();

    private TemplateManagementUseCase CreateUseCase() => new(
        _mockTemplateRepo.Object,
        _mockVersionRepo.Object,
        _mockStorage.Object,
        _mockScanner.Object,
        _mockSecurity.Object,
        _mockContext.Object,
        _mockUow.Object
    );

    // ── SaveTemplateHtmlAsync ──────────────────────────────────────────────

    [Fact]
    public async Task SaveTemplateHtmlAsync_ShouldIncrementVersion_AndUploadSnapshot()
    {
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();

        var template = new Template
        {
            Id = templateId, Name = "Contract", Slug = "contract",
            CurrentVersionId = versionId
        };
        var currentVersion = new TemplateVersion
        {
            Id = versionId, TemplateId = templateId,
            Version = 3, StorageKey = "templates/contract.html"
        };

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentVersion);
        _mockContext.Setup(c => c.CallerApp).Returns("monaco-developer");

        var request = new SaveTemplateHtmlRequest(
            Html: "<html><body>New Content</body></html>",
            ChangeNote: "Updated layout for legal terms"
        );

        int nextVersion = await CreateUseCase().SaveTemplateHtmlAsync(templateId, request);

        nextVersion.Should().Be(4);
        template.CurrentVersionId.Should().NotBeNull();

        _mockVersionRepo.Verify(r => r.AddAsync(
            It.Is<TemplateVersion>(v =>
                v.TemplateId == templateId &&
                v.Version == 4 &&
                v.Status == TemplateVersionStatus.Published &&
                v.CommitMessage == "Updated layout for legal terms"),
            It.IsAny<CancellationToken>()), Times.Once);

        // SaveTemplateHtmlAsync calls SaveChangesAsync exactly once
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveTemplateHtmlAsync_ShouldUploadToBothVersionedAndActiveKey()
    {
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template
            {
                Id = templateId, Slug = "invoice", CurrentVersionId = versionId
            });
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersion
            {
                Id = versionId, TemplateId = templateId, Version = 1,
                StorageKey = "templates/invoice.html"
            });

        await CreateUseCase().SaveTemplateHtmlAsync(templateId,
            new SaveTemplateHtmlRequest("<html></html>"));

        // Must upload both the versioned snapshot AND the active key
        _mockStorage.Verify(s => s.UploadAsync(
            StorageBuckets.Templates,
            It.Is<string>(k => k.Contains("/archive/") && k.Contains("_v2")),
            It.IsAny<Stream>(), "text/html; charset=utf-8", It.IsAny<CancellationToken>()),
            Times.Once);

        _mockStorage.Verify(s => s.UploadAsync(
            StorageBuckets.Templates,
            "templates/invoice.html",
            It.IsAny<Stream>(), "text/html; charset=utf-8", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveTemplateHtmlAsync_WhenNoCurrentVersion_ShouldThrowInvalidOperationException()
    {
        var templateId = Guid.NewGuid();
        var template   = new Template { Id = templateId, Slug = "no-ver", CurrentVersionId = null };

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateUseCase().SaveTemplateHtmlAsync(templateId, new SaveTemplateHtmlRequest("<html></html>")));
    }

    // ── CreateTemplateAsync ────────────────────────────────────────────────

    [Fact]
    public async Task CreateTemplateAsync_WithHtmlContent_ShouldCreatePublishedVersionWithHtmlFormat()
    {
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);
        _mockContext.Setup(c => c.CallerApp).Returns("system");

        var request = new CreateTemplateRequest("Sale Contract", "sale-contract", null, null);

        var dto = await CreateUseCase().CreateTemplateAsync(request);

        dto.Name.Should().Be("Sale Contract");
        dto.Slug.Should().Be("sale-contract");
        dto.CurrentVersionId.Should().NotBeNull();
        dto.FileFormat.Should().Be(TemplateFormat.Html);

        _mockVersionRepo.Verify(r => r.AddAsync(
            It.Is<TemplateVersion>(v =>
                v.Version == 1 &&
                v.Status == TemplateVersionStatus.Published &&
                v.CommitMessage == "Initial version" &&
                v.FileFormat == TemplateFormat.Html),
            It.IsAny<CancellationToken>()), Times.Once);

        // CreateTemplateAsync calls SaveChangesAsync 3 times:
        // 1. After AddAsync(template), 2. After AddAsync(version), 3. After Update(template.CurrentVersionId)
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task CreateTemplateAsync_WithDocxFile_ShouldSetDocxFormatAndUploadToTemplatesBucket()
    {
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);
        _mockSecurity.Setup(s => s.Scan(It.IsAny<Stream>()))
            .Returns(DocxScanResult.Safe());
        _mockContext.Setup(c => c.CallerApp).Returns("uploader");

        using var stream = new MemoryStream([0x50, 0x4B, 0x03, 0x04]);
        var request = new CreateTemplateRequest("DOCX Template", "docx-tpl", "contract", null);

        var dto = await CreateUseCase().CreateTemplateAsync(request, stream, "contract.docx");

        dto.FileFormat.Should().Be(TemplateFormat.Docx);

        _mockVersionRepo.Verify(r => r.AddAsync(
            It.Is<TemplateVersion>(v => v.FileFormat == TemplateFormat.Docx),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockStorage.Verify(s => s.UploadAsync(
            StorageBuckets.Templates,
            It.Is<string>(k => k.EndsWith(".docx")),
            It.IsAny<Stream>(),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTemplateAsync_WithXlsxFile_ShouldSetXlsxFormat()
    {
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);
        _mockContext.Setup(c => c.CallerApp).Returns("uploader");

        using var stream = new MemoryStream([0x50, 0x4B, 0x03, 0x04]);
        var request = new CreateTemplateRequest("Excel Template", "excel-tpl", "report", null);

        var dto = await CreateUseCase().CreateTemplateAsync(request, stream, "report.xlsx");

        dto.FileFormat.Should().Be(TemplateFormat.Xlsx);

        _mockVersionRepo.Verify(r => r.AddAsync(
            It.Is<TemplateVersion>(v => v.FileFormat == TemplateFormat.Xlsx),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockStorage.Verify(s => s.UploadAsync(
            StorageBuckets.Templates,
            It.Is<string>(k => k.EndsWith(".xlsx")),
            It.IsAny<Stream>(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTemplateAsync_WithDuplicateSlug_ShouldThrowInvalidOperationException()
    {
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template { Slug = "existing-slug" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateUseCase().CreateTemplateAsync(new CreateTemplateRequest("Duplicate", "existing-slug", null, null)));
    }

    [Fact]
    public async Task CreateTemplateAsync_WithUnsafeDocx_ShouldThrowInvalidOperationException()
    {
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);
        _mockSecurity.Setup(s => s.Scan(It.IsAny<Stream>()))
            .Returns(new DocxScanResult(false, ["VBA macro project detected."]));

        using var stream = new MemoryStream([0x50, 0x4B, 0x03, 0x04]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateUseCase().CreateTemplateAsync(
                new CreateTemplateRequest("Contract", "contract", "contract", null),
                stream, "contract.docx"));
    }

    [Fact]
    public async Task CreateTemplateAsync_WithSafeDocx_ShouldUploadWithoutException()
    {
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);
        _mockSecurity.Setup(s => s.Scan(It.IsAny<Stream>()))
            .Returns(DocxScanResult.Safe());
        _mockContext.Setup(c => c.CallerApp).Returns("test");

        using var stream = new MemoryStream([0x50, 0x4B, 0x03, 0x04]);

        var act = () => CreateUseCase().CreateTemplateAsync(
            new CreateTemplateRequest("Contract", "contract-safe", "contract", null),
            stream, "contract.docx");

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateTemplateAsync_WithNonDocxFile_ShouldNotCallSecurityScanner()
    {
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);
        _mockContext.Setup(c => c.CallerApp).Returns("test");

        using var stream = new MemoryStream("<html></html>"u8.ToArray());

        await CreateUseCase().CreateTemplateAsync(
            new CreateTemplateRequest("HTML Template", "html-tpl", null, null),
            stream, "template.html");

        _mockSecurity.Verify(s => s.Scan(It.IsAny<Stream>()), Times.Never);
    }

    // ── ListTemplatesAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ListTemplatesAsync_ShouldReturnTemplatesOrderedByUpdatedAtDesc()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var verA = Guid.NewGuid();

        var templates = new List<Template>
        {
            new() { Id = idA, Name = "Alpha", Slug = "alpha", CurrentVersionId = verA,
                    UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1) },
            new() { Id = idB, Name = "Beta",  Slug = "beta",  CurrentVersionId = null,
                    UpdatedAt = DateTimeOffset.UtcNow }
        };

        _mockTemplateRepo.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);
        _mockVersionRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<TemplateVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TemplateVersion { Id = verA, FileFormat = TemplateFormat.Html }]);

        var result = await CreateUseCase().ListTemplatesAsync();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Beta");   // newer UpdatedAt
        result[1].Name.Should().Be("Alpha");
    }

    [Fact]
    public async Task ListTemplatesAsync_ShouldIncludeFileFormatFromCurrentVersion()
    {
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        _mockTemplateRepo.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Template { Id = templateId, Name = "T", Slug = "t", CurrentVersionId = versionId }]);
        _mockVersionRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<TemplateVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TemplateVersion { Id = versionId, FileFormat = TemplateFormat.Docx }]);

        var result = await CreateUseCase().ListTemplatesAsync();

        result[0].FileFormat.Should().Be(TemplateFormat.Docx);
    }

    [Fact]
    public async Task ListTemplatesAsync_WhenTemplateHasNoVersion_ShouldReturnNullFileFormat()
    {
        _mockTemplateRepo.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Template { Id = Guid.NewGuid(), Name = "T", Slug = "t", CurrentVersionId = null }]);
        _mockVersionRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<TemplateVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateUseCase().ListTemplatesAsync();

        result[0].FileFormat.Should().BeNull();
    }

    // ── GetByIdAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ShouldReturnDtoWithFileFormatFromCurrentVersion()
    {
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template { Id = templateId, Name = "Contract", Slug = "contract", CurrentVersionId = versionId });
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersion { Id = versionId, FileFormat = TemplateFormat.Xlsx });

        var result = await CreateUseCase().GetByIdAsync(templateId);

        result.FileFormat.Should().Be(TemplateFormat.Xlsx);
        result.Name.Should().Be("Contract");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => CreateUseCase().GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByIdAsync_WhenNoCurrentVersion_ShouldReturnNullFileFormat()
    {
        var templateId = Guid.NewGuid();
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template { Id = templateId, Name = "T", Slug = "t", CurrentVersionId = null });

        var result = await CreateUseCase().GetByIdAsync(templateId);

        result.FileFormat.Should().BeNull();
        _mockVersionRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── ScanPlaceholdersAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ScanPlaceholdersAsync_ShouldDelegateToScannerWithStorageKeyExtension()
    {
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template { Id = templateId, Slug = "t", CurrentVersionId = versionId });
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersion { Id = versionId, StorageKey = "templates/t.docx" });
        _mockStorage.Setup(s => s.DownloadAsync(StorageBuckets.Templates, "templates/t.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockScanner.Setup(s => s.ScanPlaceholdersAsync(It.IsAny<Stream>(), ".docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["contractNo", "customerName"]);

        var result = await CreateUseCase().ScanPlaceholdersAsync(templateId);

        result.Should().BeEquivalentTo(["contractNo", "customerName"]);
        _mockScanner.Verify(s => s.ScanPlaceholdersAsync(It.IsAny<Stream>(), ".docx", It.IsAny<CancellationToken>()), Times.Once);
    }
}
