using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Infrastructure.Engines.Html;
using Xunit;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Tests.Infrastructure.Engines.Html;

public class HtmlDirectGenerationTests
{
    private readonly Mock<ITemplateRepository>         _mockTemplateRepo    = new();
    private readonly Mock<IRepository<TemplateVersion>> _mockVersionRepo     = new();
    private readonly Mock<IDatasetRepository>           _mockDatasetRepo     = new();
    private readonly Mock<IDataConnectionRepository>    _mockConnectionRepo  = new();
    private readonly Mock<IRepository<GenerationLog>>   _mockLogRepo         = new();
    private readonly Mock<IRepository<Document>>        _mockDocumentRepo    = new();
    private readonly Mock<IRepository<DocumentVersion>> _mockDocVersionRepo  = new();
    private readonly Mock<IStorageService>              _mockStorage         = new();
    private readonly Mock<IExecutionContext>             _mockContext         = new();
    private readonly Mock<IUnitOfWork>                  _mockUow             = new();
    private readonly Mock<IFieldMappingApplicatorService> _mockApplicator    = new();
    private readonly Mock<IDataProtectionService>       _mockDataProtection  = new();
    private readonly Mock<IPdfRenderer>                 _mockPdfRenderer     = new();
    private readonly Mock<IQrCodeService>               _mockQrService       = new();
    private readonly Mock<IBarcodeService>              _mockBarcodeService  = new();
    private readonly Mock<IJsonSchemaValidationService> _mockSchemaValidation = new();

    [Fact]
    public async Task ExecuteAsync_HtmlTemplateWithoutMappings_ShouldDirectlyRenderNestedJsonWithHandlebars()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();
        string capturedHtml = string.Empty;

        var template = new Template(Guid.NewGuid(), "Direct HTML Invoice", "invoice-direct-html", null, id: templateId);
        template.SetCurrentVersion(versionId);

        var version = new TemplateVersion(templateId, 1, "templates/invoice.html", TemplateFormat.Html, "Published", "Initial", id: versionId);

        string htmlContent = @"
            <html>
            <body>
                <h1>{{companyName}}</h1>
                <div>Customer: {{customer.fullName}}</div>
                <div>Status: 
                    {{#ifEquals status 'APPROVED'}}
                        <span class='badge-success'>อนุมัติแล้ว</span>
                    {{else}}
                        <span class='badge-pending'>รออนุมัติ</span>
                    {{/ifEquals}}
                </div>
                <table>
                    <tbody>
                        {{#each items}}
                        <tr>
                            <td>{{addOne @index}}</td>
                            <td>{{name}}</td>
                            <td>{{amount:number}}</td>
                        </tr>
                        {{/each}}
                    </tbody>
                </table>
                <div>Total: {{totalAmount:thai_baht_text}}</div>
            </body>
            </html>";

        _mockContext.Setup(c => c.ProjectId).Returns(template.ProjectId);
        _mockTemplateRepo.Setup(r => r.GetBySlugWithDetailsAsync("invoice-direct-html", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockTemplateRepo.Setup(r => r.GetBySlugAsync("invoice-direct-html", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes(htmlContent)));

        _mockStorage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("outputs/result.pdf");

        _mockStorage.Setup(s => s.GetPresignedUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://minio.local/outputs/result.pdf");

        _mockPdfRenderer.Setup(p => p.RenderHtmlToPdfAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, string?, CancellationToken>((h, _1, _2, _3) => capturedHtml = h)
            .ReturnsAsync(Encoding.UTF8.GetBytes("%PDF-1.4 Mock PDF"));

        var mediaService = new SmkDoc.Infrastructure.Imaging.MediaGenerationService(_mockQrService.Object, _mockBarcodeService.Object, new SmkDoc.Infrastructure.Imaging.ImageOptimizerService());
        var htmlEngine = new HtmlTemplateEngine(_mockPdfRenderer.Object, new SmkDoc.Infrastructure.Engines.Html.Helpers.HtmlHelperRegistry(mediaService));

        var dataPrep = new SmkDoc.Application.Modules.Rendering.Documents.Services.DocumentDataPreparationService(
            _mockDatasetRepo.Object,
            _mockConnectionRepo.Object,
            _mockDataProtection.Object,
            _mockApplicator.Object,
            _mockSchemaValidation.Object);
        var audit = new SmkDoc.Application.Modules.Rendering.Documents.Services.DocumentAuditService(
            _mockLogRepo.Object, _mockContext.Object, _mockUow.Object);
        var versioning = new SmkDoc.Application.Modules.Rendering.Documents.Services.DocumentVersioningService(
            _mockDocumentRepo.Object, _mockDocVersionRepo.Object, _mockContext.Object, _mockUow.Object);

        var useCase = new GenerateDocumentUseCase(
            _mockTemplateRepo.Object,
            _mockVersionRepo.Object,
            _mockStorage.Object,
            new IRenderEngine[] { htmlEngine },
            dataPrep,
            audit,
            versioning,
            _mockUow.Object,
            _mockContext.Object
        );

        var payload = JsonDocument.Parse(@"{
            ""companyName"": ""บริษัท สัมมากร จำกัด (มหาชน)"",
            ""status"": ""APPROVED"",
            ""customer"": {
                ""fullName"": ""คุณสมชาย มีสุข""
            },
            ""items"": [
                { ""name"": ""บ้านเดี่ยว ชัยพฤกษ์"", ""amount"": 5500000.50 },
                { ""name"": ""ค่าบริการส่วนกลาง"", ""amount"": 25000 }
            ],
            ""totalAmount"": 5525000.50
        }").RootElement;

        var request = new GenerateDocumentCommand(payload, Output: "pdf", DocumentRef: "INV-2026-0001");

        // Act
        var response = await useCase.ExecuteAsync("invoice-direct-html", request, CancellationToken.None);

        // Assert
        response.Should().NotBeNull();
        response.Url.Should().Be("https://minio.local/outputs/result.pdf");

        // Verify HTML received nested fields, loops, conditions, and Thai transform correctly
        capturedHtml.Should().Contain("บริษัท สัมมากร จำกัด (มหาชน)");
        capturedHtml.Should().Contain("Customer: คุณสมชาย มีสุข");
        capturedHtml.Should().Contain("<span class='badge-success'>อนุมัติแล้ว</span>");
        capturedHtml.Should().NotContain("<span class='badge-pending'>รออนุมัติ</span>");
        capturedHtml.Should().Contain("<td>1</td>");
        capturedHtml.Should().Contain("<td>บ้านเดี่ยว ชัยพฤกษ์</td>");
        capturedHtml.Should().Contain("<td>5,500,000.50</td>");
        capturedHtml.Should().Contain("<td>2</td>");
        capturedHtml.Should().Contain("<td>ค่าบริการส่วนกลาง</td>");
        capturedHtml.Should().Contain("<td>25,000.00</td>");
        capturedHtml.Should().Contain("ห้าล้านห้าแสนสองหมื่นห้าพันบาทห้าสิบสตางค์");

        // Verify Applicator was NOT invoked since mappings were 0
        _mockApplicator.Verify(a => a.ApplyAsync(It.IsAny<JsonElement>(), It.IsAny<IEnumerable<FieldMapping>>(), It.IsAny<IReadOnlyDictionary<string, ResolvedDatasetContext>>()), Times.Never);
    }
}
