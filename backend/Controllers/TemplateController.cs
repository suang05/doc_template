using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDocServer.Application.Services;
using SmkDocServer.Domain.Entities;
using SmkDocServer.Domain.Interfaces;

namespace SmkDocServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TemplateController : ControllerBase
{
    private readonly ITemplateStorageService _storageService;
    private readonly ITemplateResolverService _resolverService;
    private readonly ITemplateVersioningService _versioningService;
    private readonly ITemplateSchemaService _schemaService;
    private readonly FieldMappingService _mappingService;

    public TemplateController(
        ITemplateStorageService storageService,
        ITemplateResolverService resolverService,
        ITemplateVersioningService versioningService,
        ITemplateSchemaService schemaService,
        FieldMappingService mappingService)
    {
        _storageService = storageService;
        _resolverService = resolverService;
        _versioningService = versioningService;
        _schemaService = schemaService;
        _mappingService = mappingService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] bool isGlobal = false)
    {
        try
        {
            var project = HttpContext.Items["Project"] as Project;
            // Only allow setting isGlobal = true if user is from default/admin project or explicitly authorized
            bool allowGlobal = isGlobal && (project == null || project.Code == "SMK_DEFAULT");
            var metadata = await _storageService.SaveTemplateAsync(file, project?.Id, allowGlobal);

            return Ok(new { 
                Message = "Template uploaded successfully", 
                FileName = metadata.FileName,
                StoragePath = metadata.StoragePath,
                IsGlobal = metadata.IsGlobal,
                ProjectCode = project?.Code ?? "GLOBAL"
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error uploading template", Details = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? projectId = null)
    {
        var currentProject = HttpContext.Items["Project"] as Project;
        // If current project is SMK_DEFAULT, allow querying any project or all
        Guid? effectiveProjectId = (currentProject != null && currentProject.Code == "SMK_DEFAULT" && projectId.HasValue) 
            ? projectId.Value 
            : currentProject?.Id;

        var templates = await _resolverService.ListTemplatesAsync(effectiveProjectId);
        return Ok(new { 
            Templates = templates.Select(t => t.FileName).Distinct(), 
            Details = templates.Select(t => new {
                t.Id,
                t.FileName,
                t.OriginalName,
                t.Format,
                t.StoragePath,
                t.IsGlobal,
                t.ProjectId,
                ProjectCode = t.Project?.Code,
                Version = t.CurrentVersion,
                VersionsCount = t.Versions != null && t.Versions.Count > 0 ? t.Versions.Count : 1,
                UploadedAt = t.UploadedAt,
                CreatedAt = t.UploadedAt
            }), 
            Count = templates.Count() 
        });
    }

    [HttpGet("{fileName}/versions")]
    public async Task<IActionResult> GetVersions(string fileName)
    {
        try
        {
            var project = HttpContext.Items["Project"] as Project;
            var versions = await _versioningService.GetVersionsAsync(fileName, project?.Id);
            return Ok(new {
                FileName = fileName,
                Versions = versions,
                Count = versions.Count()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error fetching template versions", Details = ex.Message });
        }
    }

    [HttpPost("{fileName}/rollback/{version}")]
    public async Task<IActionResult> Rollback(string fileName, int version)
    {
        try
        {
            var project = HttpContext.Items["Project"] as Project;
            var result = await _versioningService.RollbackAsync(fileName, version, project?.Id);
            return Ok(new {
                Message = $"Template '{fileName}' rolled back to version {version} successfully",
                NewVersion = result.CurrentVersion,
                FileName = result.FileName
            });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error rolling back template", Details = ex.Message });
        }
    }

    [HttpGet("{fileName}/schema")]
    public async Task<IActionResult> GetSchema(string fileName)
    {
        try
        {
            var project = HttpContext.Items["Project"] as Project;
            var schema = await _schemaService.InspectSchemaAsync(fileName, project?.Id);
            return Ok(schema);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error inspecting template schema", Details = ex.Message });
        }
    }

    [HttpGet("{fileName}/download")]
    [HttpHead("{fileName}/download")]
    public async Task<IActionResult> Download(string fileName)
    {
        try
        {
            var project = HttpContext.Items["Project"] as Project;
            string? localPath = await _resolverService.ResolveTemplatePathAsync(fileName, project?.Id);
            if (string.IsNullOrEmpty(localPath) || !System.IO.File.Exists(localPath))
            {
                return NotFound(new { Message = $"Template '{fileName}' not found." });
            }

            string ext = Path.GetExtension(localPath).ToLowerInvariant();
            string contentType = ext == ".docx"
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            var fileBytes = await System.IO.File.ReadAllBytesAsync(localPath);
            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error downloading template", Details = ex.Message });
        }
    }

    [HttpDelete("{fileName}")]
    public async Task<IActionResult> Delete(string fileName)
    {
        try
        {
            var project = HttpContext.Items["Project"] as Project;
            await _storageService.DeleteTemplateAsync(fileName, project?.Id);
            return Ok(new { Message = $"Template '{fileName}' deleted successfully" });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error deleting template", Details = ex.Message });
        }
    }

    public class TiptapSaveDto
    {
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsGlobal { get; set; } = false;
    }

    [HttpPost("tiptap")]
    public async Task<IActionResult> SaveTiptap([FromBody] TiptapSaveDto body)
    {
        if (string.IsNullOrWhiteSpace(body.FileName) || string.IsNullOrWhiteSpace(body.Content))
            return BadRequest(new { Message = "FileName และ Content ต้องไม่ว่าง" });

        try
        {
            var project = HttpContext.Items["Project"] as Project;
            bool allowGlobal = body.IsGlobal && (project == null || project.Code == "SMK_DEFAULT");
            var metadata = await _storageService.SaveHtmlTemplateAsync(body.FileName, body.Content, project?.Id, allowGlobal);
            return Ok(new {
                Message = "บันทึก Tiptap Template สำเร็จ",
                FileName = metadata.FileName,
                StoragePath = metadata.StoragePath,
                EngineType = metadata.EngineType.ToString()
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error saving Tiptap template", Details = ex.Message });
        }
    }

    public class ReportBroSaveDto
    {
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsGlobal { get; set; } = false;
    }

    [HttpPost("reportbro")]
    public async Task<IActionResult> SaveReportBro([FromBody] ReportBroSaveDto body)
    {
        if (string.IsNullOrWhiteSpace(body.FileName) || string.IsNullOrWhiteSpace(body.Content))
            return BadRequest(new { Message = "FileName และ Content ต้องไม่ว่าง" });

        try
        {
            var project = HttpContext.Items["Project"] as Project;
            bool allowGlobal = body.IsGlobal && (project == null || project.Code == "SMK_DEFAULT");
            var metadata = await _storageService.SaveJsonTemplateAsync(body.FileName, body.Content, project?.Id, allowGlobal);
            return Ok(new {
                Message = "บันทึก ReportBro Template สำเร็จ",
                FileName = metadata.FileName,
                StoragePath = metadata.StoragePath,
                EngineType = metadata.EngineType.ToString()
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error saving ReportBro template", Details = ex.Message });
        }
    }

    [HttpGet("{fileName}/mappings")]
    public async Task<IActionResult> GetMappings(string fileName)
    {
        try
        {
            var project = HttpContext.Items["Project"] as Project;
            var template = await _resolverService.GetMetadataAsync(fileName, project?.Id);
            if (template == null)
            {
                return NotFound(new { Message = $"ไม่พบแม่แบบ '{fileName}'" });
            }

            var mappings = await _mappingService.GetMappingsAsync(template.Id);
            return Ok(new { TemplateId = template.Id, FileName = fileName, Mappings = mappings });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error retrieving field mappings", Details = ex.Message });
        }
    }

    [HttpPut("{fileName}/mappings")]
    public async Task<IActionResult> SaveMappings(string fileName, [FromBody] List<FieldMappingDto> mappings)
    {
        try
        {
            var project = HttpContext.Items["Project"] as Project;
            var template = await _resolverService.GetMetadataAsync(fileName, project?.Id);
            if (template == null)
            {
                return NotFound(new { Message = $"ไม่พบแม่แบบ '{fileName}'" });
            }

            var saved = await _mappingService.SaveMappingsAsync(template.Id, mappings);
            return Ok(new { Message = "บันทึก Field Mappings สำเร็จ", TemplateId = template.Id, FileName = fileName, Mappings = saved });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error saving field mappings", Details = ex.Message });
        }
    }

    public class PreviewMappingRequestDto
    {
        public List<FieldMappingDto> Mappings { get; set; } = new();
        public System.Text.Json.JsonElement SampleData { get; set; }
    }

    [HttpPost("{fileName}/mappings/preview")]
    public IActionResult PreviewMappings(string fileName, [FromBody] PreviewMappingRequestDto body)
    {
        try
        {
            var results = _mappingService.PreviewMappings(body.Mappings, body.SampleData);
            return Ok(new { FileName = fileName, Preview = results });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error previewing field mappings", Details = ex.Message });
        }
    }
}
