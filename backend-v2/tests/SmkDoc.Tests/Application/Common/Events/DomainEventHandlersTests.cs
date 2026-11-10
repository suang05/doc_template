using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.EventHandlers;
using SmkDoc.Application.Modules.IdentityAccess.Security.EventHandlers;
using SmkDoc.Application.Modules.IdentityAccess.Users.EventHandlers;
using SmkDoc.Application.Modules.Rendering.Documents.EventHandlers;
using SmkDoc.Domain.Events;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Common.Events;

public sealed class DomainEventHandlersTests
{
    private readonly ISecurityAuditService _auditService = Substitute.For<ISecurityAuditService>();

    [Fact]
    public async Task TemplatePublishedEventHandler_DelegatesToSecurityAuditService()
    {
        // Arrange
        var templateId = TemplateId.New();
        var projectId = ProjectId.New();
        var versionId = Guid.NewGuid();
        var evt = new TemplatePublishedDomainEvent(templateId, projectId, versionId, 3, TestConstants.BaselineTime);
        var sut = new TemplatePublishedEventHandler(_auditService);

        // Act
        await sut.HandleAsync(evt);

        // Assert
        _auditService.Received(1).LogTemplatePublished(projectId.Value, templateId.Value, versionId, 3);
    }

    [Fact]
    public async Task DocumentVersionCreatedEventHandler_DelegatesToSecurityAuditService()
    {
        // Arrange
        var documentId = DocumentId.New();
        var projectId = ProjectId.New();
        var genLogId = Guid.NewGuid();
        var evt = new DocumentVersionCreatedDomainEvent(documentId, projectId, 2, Guid.NewGuid(), genLogId, TestConstants.BaselineTime);
        var sut = new DocumentVersionCreatedEventHandler(_auditService);

        // Act
        await sut.HandleAsync(evt);

        // Assert
        _auditService.Received(1).LogDocumentVersionCreated(projectId.Value, documentId.Value, 2, genLogId);
    }

    [Fact]
    public async Task ApiKeyRevokedEventHandler_DelegatesToSecurityAuditService()
    {
        // Arrange
        var apiKeyId = ApiKeyId.New();
        var projectId = ProjectId.New();
        var evt = new ApiKeyRevokedDomainEvent(apiKeyId, projectId, TestConstants.BaselineTime);
        var sut = new ApiKeyRevokedEventHandler(_auditService);

        // Act
        await sut.HandleAsync(evt);

        // Assert
        _auditService.Received(1).LogApiKeyRevoked(projectId.Value, apiKeyId.Value);
    }

    [Fact]
    public async Task UserDeactivatedEventHandler_DelegatesToSecurityAuditService()
    {
        // Arrange
        var userId = UserId.New();
        var evt = new UserDeactivatedDomainEvent(userId, TestConstants.BaselineTime);
        var sut = new UserDeactivatedEventHandler(_auditService);

        // Act
        await sut.HandleAsync(evt);

        // Assert
        _auditService.Received(1).LogUserDeactivated(userId.Value);
    }
}
