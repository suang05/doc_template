using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.Documents;
using SmkDoc.Application.DTOs.Templates;
using SmkDoc.Application.UseCases.Templates;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using Xunit;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Tests.Application.UseCases.Templates;

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

        var template = new Template(Guid.NewGuid(), "Contract", "contract", null) { Id = templateId };
        template.SetCurrentVersion(versionId);
        var currentVersion = new TemplateVersion(templateId, 3, "templates/contract.html", TemplateFormat.Html, "Published", "Commit") { Id = versionId };

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentVersion);
        _mockContext.Setup(c => c.CallerApp).Returns("monaco-developer");

        var request = new SaveTemplateHtmlCommand(
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

        // SaveTemplateHtmlAsync calls CommitAsync exactly once
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveTemplateHtmlAsync_ShouldUploadToBothVersionedAndActiveKey()
    {
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();

        var template = new Template(Guid.NewGuid(), "invoice", "invoice", null) { Id = templateId };
        template.SetCurrentVersion(versionId);

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersion(templateId, 1, "templates/invoice.html", TemplateFormat.Html, "Published", "Commit") { Id = versionId });

        await CreateUseCase().SaveTemplateHtmlAsync(templateId,
            new SaveTemplateHtmlCommand("<html></html>"));

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
        var template = new Template(Guid.NewGuid(), "no-ver", "no-ver", null) { Id = templateId };

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var act = () => CreateUseCase().SaveTemplateHtmlAsync(templateId, new SaveTemplateHtmlCommand("<html></html>"));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── CreateTemplateAsync ────────────────────────────────────────────────

    [Fact]
    public async Task CreateTemplateAsync_WithHtmlContent_ShouldCreatePublishedVersionWithHtmlFormat()
    {
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);
        _mockContext.Setup(c => c.CallerApp).Returns("system");

        var request = new CreateTemplateCommand("Sale Contract", "sale-contract", null, null);

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

        // CreateTemplateAsync calls CommitAsync 3 times:
        // 1. After AddAsync(template), 2. After AddAsync(version), 3. After Update(template.CurrentVersionId)
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
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
        var request = new CreateTemplateCommand("DOCX Template", "docx-tpl", "contract", null);

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
        var request = new CreateTemplateCommand("Excel Template", "excel-tpl", "report", null);

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
    public async Task CreateTemplateAsync_WithDuplicateSlug_ShouldThrowConflictException()
    {
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template(Guid.NewGuid(), "existing-slug", "existing-slug", null));

        var act = () => CreateUseCase().CreateTemplateAsync(new CreateTemplateCommand("Duplicate", "existing-slug", null, null));
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CreateTemplateAsync_WithUnsafeDocx_ShouldThrowInvalidOperationException()
    {
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);
        _mockSecurity.Setup(s => s.Scan(It.IsAny<Stream>()))
            .Returns(new DocxScanResult(false, ["VBA macro project detected."]));

        using var stream = new MemoryStream([0x50, 0x4B, 0x03, 0x04]);

        var act = () => CreateUseCase().CreateTemplateAsync(
            new CreateTemplateCommand("Contract", "contract", "contract", null),
            stream, "contract.docx");
        await act.Should().ThrowAsync<InvalidOperationException>();
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
            new CreateTemplateCommand("Contract", "contract-safe", "contract", null),
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
            new CreateTemplateCommand("HTML Template", "html-tpl", null, null),
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

        var tA = new Template(Guid.NewGuid(), "Alpha", "alpha", null) { Id = idA };
        tA.SetCurrentVersion(verA);
        var tB = new Template(Guid.NewGuid(), "Beta", "beta", null) { Id = idB };
        tB.UpdateDetails("Beta", "beta"); // This triggers SetUpdated() so UpdatedAt is now > tA.CreatedAt

        var templates = new List<Template> { tA, tB };

        _mockTemplateRepo.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);
        _mockVersionRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<TemplateVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TemplateVersion(Guid.NewGuid(), 1, "key", TemplateFormat.Html, "Published", "Commit") { Id = verA }]);

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

        var template = new Template(Guid.NewGuid(), "T", "t", null) { Id = templateId };
        template.SetCurrentVersion(versionId);

        _mockTemplateRepo.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([template]);
        _mockVersionRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<TemplateVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TemplateVersion(Guid.NewGuid(), 1, "key", TemplateFormat.Docx, "Published", "Commit") { Id = versionId }]);

        var result = await CreateUseCase().ListTemplatesAsync();

        result[0].FileFormat.Should().Be(TemplateFormat.Docx);
    }

    [Fact]
    public async Task ListTemplatesAsync_WhenTemplateHasNoVersion_ShouldReturnNullFileFormat()
    {
        _mockTemplateRepo.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Template(Guid.NewGuid(), "T", "t", null) { Id = Guid.NewGuid() }]);
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

        var template = new Template(Guid.NewGuid(), "Contract", "contract", null) { Id = templateId };
        template.SetCurrentVersion(versionId);

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersion(Guid.NewGuid(), 1, "key", TemplateFormat.Xlsx, "Published", "Commit") { Id = versionId });

        var result = await CreateUseCase().GetByIdAsync(templateId);

        result.FileFormat.Should().Be(TemplateFormat.Xlsx);
        result.Name.Should().Be("Contract");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ShouldThrowNotFoundException()
    {
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        var act = () => CreateUseCase().GetByIdAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNoCurrentVersion_ShouldReturnNullFileFormat()
    {
        var templateId = Guid.NewGuid();
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template(Guid.NewGuid(), "T", "t", null) { Id = templateId });

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

        var template = new Template(Guid.NewGuid(), "T", "t", null) { Id = templateId };
        template.SetCurrentVersion(versionId);

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersion(Guid.NewGuid(), 1, "templates/t.docx", TemplateFormat.Docx, "Published", "Commit") { Id = versionId });
        _mockStorage.Setup(s => s.DownloadAsync(StorageBuckets.Templates, "templates/t.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockScanner.Setup(s => s.ScanPlaceholdersAsync(It.IsAny<Stream>(), ".docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["contractNo", "customerName"]);

        var result = await CreateUseCase().ScanPlaceholdersAsync(templateId);

        result.Should().BeEquivalentTo(["contractNo", "customerName"]);
        _mockScanner.Verify(s => s.ScanPlaceholdersAsync(It.IsAny<Stream>(), ".docx", It.IsAny<CancellationToken>()), Times.Once);
    }
}
