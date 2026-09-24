using System.Text;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.Engines;
using SmkDoc.Application.UseCases.Templates;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using Xunit;

namespace SmkDoc.Tests;

public class TemplateDraftUseCaseTests
{
    // ── shared mocks ─────────────────────────────────────────────────────────
    private readonly Mock<ITemplateScannerService>   _scanner    = new();
    private readonly Mock<ITemplateDraftCache>        _cache      = new();
    private readonly Mock<IRenderEngine>              _engine     = new();
    private readonly Mock<IStorageService>            _storage    = new();
    private readonly Mock<IRepository<Template>>      _templateRepo = new();
    private readonly Mock<IRepository<TemplateVersion>> _versionRepo = new();
    private readonly Mock<IRepository<FieldMapping>> _mappingRepo = new();
    private readonly Mock<IUnitOfWork>               _uow         = new();

    private TemplateDraftUseCase BuildUseCase() => new(
        _scanner.Object,
        _cache.Object,
        new[] { _engine.Object },
        _storage.Object,
        _templateRepo.Object,
        _versionRepo.Object,
        _mappingRepo.Object,
        _uow.Object
    );

    // ── ParseAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_ValidFile_StoresDraftAndReturnsPlaceholders()
    {
        // Arrange
        var placeholders = new List<string> { "customer.name", "invoice.total" };
        _scanner.Setup(s => s.ScanPlaceholdersAsync(It.IsAny<Stream>(), ".docx", It.IsAny<CancellationToken>()))
                .ReturnsAsync(placeholders);
        _cache.Setup(c => c.StoreAsync(It.IsAny<TemplateDraftEntry>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("draft-abc");

        var useCase = BuildUseCase();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake-docx"));

        // Act
        var result = await useCase.ParseAsync(stream, "invoice.docx", CancellationToken.None);

        // Assert
        result.DraftId.Should().Be("draft-abc");
        result.Placeholders.Should().BeEquivalentTo(placeholders);
        _cache.Verify(c => c.StoreAsync(It.Is<TemplateDraftEntry>(e =>
            e.FileExtension == ".docx" && e.FileName == "invoice.docx"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ParseAsync_ScannerReturnsEmpty_StoresEntryWithEmptyPlaceholders()
    {
        _scanner.Setup(s => s.ScanPlaceholdersAsync(It.IsAny<Stream>(), ".html", It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        _cache.Setup(c => c.StoreAsync(It.IsAny<TemplateDraftEntry>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("draft-empty");

        var useCase = BuildUseCase();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<html></html>"));

        var result = await useCase.ParseAsync(stream, "empty.html", CancellationToken.None);

        result.Placeholders.Should().BeEmpty();
        result.DraftId.Should().Be("draft-empty");
    }

    // ── PreviewAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PreviewAsync_ValidDraftId_RendersWithoutSideEffects()
    {
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-preview");
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("content"), "t.docx", ".docx", ["field"]);

        _cache.Setup(c => c.GetAsync("draft-1", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _engine.Setup(e => e.EngineType).Returns(RenderEngineType.Docx);
        _engine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
               .ReturnsAsync(pdfBytes);

        var useCase = BuildUseCase();

        var result = await useCase.PreviewAsync("draft-1", "{\"field\":\"val\"}", CancellationToken.None);

        result.Should().BeEquivalentTo(pdfBytes);
        // Zero side-effects — no MinIO writes, no DB saves, no cache eviction:
        _storage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PreviewAsync_DraftNotFound_ThrowsDraftExpiredException()
    {
        _cache.Setup(c => c.GetAsync("gone", It.IsAny<CancellationToken>())).ReturnsAsync((TemplateDraftEntry?)null);

        var useCase = BuildUseCase();

        await useCase.Invoking(u => u.PreviewAsync("gone", "{}", CancellationToken.None))
                     .Should().ThrowAsync<DraftExpiredException>();
    }

    // ── CommitAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CommitAsync_HappyPath_UploadsToMinioThenWritesDb()
    {
        var fileBytes = Encoding.UTF8.GetBytes("xlsx-content");
        var entry = new TemplateDraftEntry(fileBytes, "report.xlsx", ".xlsx", []);
        var request = new CommitDraftRequest("Report", "report", "finance", []);

        _cache.Setup(c => c.GetAsync("draft-commit", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("storage-key");
        _templateRepo.Setup(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _cache.Setup(c => c.RemoveAsync("draft-commit", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var useCase = BuildUseCase();
        var templateId = await useCase.CommitAsync("draft-commit", request, CancellationToken.None);

        templateId.Should().NotBeEmpty();
        _storage.Verify(s => s.UploadAsync(StorageBuckets.Templates, It.IsAny<string>(), It.IsAny<Stream>(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _cache.Verify(c => c.RemoveAsync("draft-commit", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitAsync_DbWriteFails_RollsBackMinioUpload()
    {
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("data"), "t.docx", ".docx", []);
        var request = new CommitDraftRequest("T", "t", null, []);

        _cache.Setup(c => c.GetAsync("draft-fail", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("storage-key");
        _templateRepo.Setup(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("DB error"));
        _storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var useCase = BuildUseCase();

        await useCase.Invoking(u => u.CommitAsync("draft-fail", request, CancellationToken.None))
                     .Should().ThrowAsync<Exception>().WithMessage("DB error");

        _storage.Verify(s => s.DeleteAsync(StorageBuckets.Templates, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitAsync_DraftNotFound_ThrowsDraftExpiredException()
    {
        _cache.Setup(c => c.GetAsync("stale", It.IsAny<CancellationToken>())).ReturnsAsync((TemplateDraftEntry?)null);

        var useCase = BuildUseCase();
        var request = new CommitDraftRequest("T", "t", null, []);

        await useCase.Invoking(u => u.CommitAsync("stale", request, CancellationToken.None))
                     .Should().ThrowAsync<DraftExpiredException>();
    }

    [Fact]
    public async Task CommitAsync_WithMappings_PersistsAllFieldMappings()
    {
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("html"), "t.html", ".html", []);
        var mappings = new List<SaveFieldMappingItem>
        {
            new("name", "customer.name", "ชื่อลูกค้า", true, null, null, 0),
            new("total", "invoice.total", "ยอดรวม", false, null, null, 1),
        };
        var request = new CommitDraftRequest("Invoice", "invoice", null, mappings);

        _cache.Setup(c => c.GetAsync("draft-map", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("key");
        _templateRepo.Setup(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mappingRepo.Setup(r => r.AddAsync(It.IsAny<FieldMapping>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var useCase = BuildUseCase();
        await useCase.CommitAsync("draft-map", request, CancellationToken.None);

        _mappingRepo.Verify(r => r.AddAsync(It.IsAny<FieldMapping>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
