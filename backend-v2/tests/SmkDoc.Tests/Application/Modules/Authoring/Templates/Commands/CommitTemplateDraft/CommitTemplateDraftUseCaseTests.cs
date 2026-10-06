using System.Text;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;

public class CommitTemplateDraftUseCaseTests
{
    private readonly Mock<ITemplateDraftCache> _cache = new();
    private readonly Mock<IStorageService> _storage = new();
    private readonly Mock<ITemplateRepository> _templateRepo = new();
    private readonly Mock<IRepository<TemplateVersion>> _versionRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private CommitTemplateDraftUseCase CreateSut() =>
        new(_cache.Object, _storage.Object, _templateRepo.Object, _versionRepo.Object, _uow.Object);

    [Fact]
    public async Task CommitAsync_HappyPath_UploadsToMinioThenWritesDb()
    {
        // Arrange
        var fileBytes = Encoding.UTF8.GetBytes("xlsx-content");
        var entry = new TemplateDraftEntry(fileBytes, "report.xlsx", ".xlsx", []);
        var request = new CommitDraftCommand("Report", "report", "finance", [], ProjectId: Guid.NewGuid());

        _cache.Setup(c => c.GetAsync("draft-commit", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storage-key");
        _templateRepo.Setup(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _cache.Setup(c => c.RemoveAsync("draft-commit", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var templateId = await CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-commit", request), CancellationToken.None);

        // Assert
        templateId.Should().NotBeEmpty();
        _storage.Verify(s => s.UploadAsync(StorageBuckets.Templates, It.IsAny<string>(), It.IsAny<Stream>(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _cache.Verify(c => c.RemoveAsync("draft-commit", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitAsync_DbWriteFails_RollsBackMinioUpload()
    {
        // Arrange
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("data"), "t.docx", ".docx", []);
        var request = new CommitDraftCommand("T", "test-template", null, [], ProjectId: Guid.NewGuid());

        _cache.Setup(c => c.GetAsync("draft-fail", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storage-key");
        _templateRepo.Setup(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("DB error"));
        _storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(new CommitTemplateDraftCommand("draft-fail", request), CancellationToken.None))
            .Should().ThrowAsync<Exception>().WithMessage("DB error");

        _storage.Verify(s => s.DeleteAsync(StorageBuckets.Templates, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CommitAsync_DraftNotFound_ThrowsDraftExpiredException()
    {
        // Arrange
        _cache.Setup(c => c.GetAsync("stale", It.IsAny<CancellationToken>())).ReturnsAsync((TemplateDraftEntry?)null);
        var request = new CommitDraftCommand("T", "test-template", null, [], ProjectId: Guid.NewGuid());

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(new CommitTemplateDraftCommand("stale", request), CancellationToken.None))
            .Should().ThrowAsync<DraftExpiredException>();
    }

    [Fact]
    public async Task CommitAsync_WithMappings_PersistsAllFieldMappings()
    {
        // Arrange
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("html"), "t.html", ".html", []);
        var mappings = new List<SaveFieldMappingItemDto>
        {
            new("name", "customer.name", "ชื่อลูกค้า", true, null, null, 0),
            new("total", "invoice.total", "ยอดรวม", false, null, null, 1),
        };
        var request = new CommitDraftCommand("Invoice", "invoice", null, mappings, ProjectId: Guid.NewGuid());

        _cache.Setup(c => c.GetAsync("draft-map", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("key");
        _templateRepo.Setup(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-map", request), CancellationToken.None);

        // Assert
        _templateRepo.Verify(r => r.AddAsync(
            It.Is<Template>(t => t.FieldMappings.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
