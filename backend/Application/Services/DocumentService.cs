using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SmkDocServer.Domain.Entities;
using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Domain.Models;
using SmkDocServer.Infrastructure.Services.Storage;

namespace SmkDocServer.Application.Services;

public class DocumentService : IDocumentService
{
    private readonly IEnumerable<ITemplateProcessor> _processors;
    private readonly IMinioStorageService _minioStorageService;
    private readonly MinioSettings _minioSettings;
    private readonly IGenerationLogService _logService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITemplateResolverService _templateResolver;
    private readonly FieldMappingService _fieldMappingService;
    private readonly IPdfConverter _pdfConverter;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IEnumerable<ITemplateProcessor> processors,
        IMinioStorageService minioStorageService,
        IOptions<MinioSettings> minioOptions,
        IGenerationLogService logService,
        IHttpContextAccessor httpContextAccessor,
        ITemplateResolverService templateResolver,
        FieldMappingService fieldMappingService,
        IPdfConverter pdfConverter,
        ILogger<DocumentService> logger)
    {
        _processors = processors;
        _minioStorageService = minioStorageService;
        _minioSettings = minioOptions.Value;
        _logService = logService;
        _httpContextAccessor = httpContextAccessor;
        _templateResolver = templateResolver;
        _fieldMappingService = fieldMappingService;
        _pdfConverter = pdfConverter;
        _logger = logger;
    }

    public async Task<DocumentGenerationResponse> GenerateAndStoreDocumentAsync(DocumentGenerationRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var httpContext = _httpContextAccessor.HttpContext;
        var project = httpContext?.Items["Project"] as Project;
        var apiKey = httpContext?.Items["ApiKey"] as ApiKey;
        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext?.Request.Headers["User-Agent"].ToString();

        var log = new GenerationLog
        {
            Id = Guid.NewGuid(),
            ProjectId = project?.Id,
            ApiKeyId = apiKey?.Id,
            TemplateName = request.TemplateName ?? "unknown",
            OutputFormat = (request.OutputFormat ?? "docx").ToLower(),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Status = "PROCESSING"
        };

        try
        {
            if (string.IsNullOrWhiteSpace(request.TemplateName))
            {
                throw new ArgumentException("TemplateName is required.", nameof(request.TemplateName));
            }

            string? templatePath = await _templateResolver.ResolveTemplatePathAsync(request.TemplateName, project?.Id);
            if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Template '{request.TemplateName}' not found for project '{project?.Code ?? "GLOBAL"}'.");
            }

            string extension = Path.GetExtension(templatePath).ToLower();
            var processingData = ConvertToDocumentProcessingData(request);

            // Resolve template metadata (needed for EngineType routing + field mappings)
            TemplateMetadata? templateMeta = null;
            bool mappingsApplied = false;
            try
            {
                templateMeta = await _templateResolver.GetMetadataAsync(request.TemplateName, project?.Id);
                if (templateMeta != null)
                {
                    using var doc = JsonDocument.Parse(JsonSerializer.Serialize(request.Data));
                    var mappedData = await _fieldMappingService.TryApplyMappingAsync(templateMeta.Id, doc.RootElement);
                    if (mappedData != null)
                    {
                        bool hasAnyData = mappedData.Replace.Count > 0 || mappedData.Table.Count > 0 ||
                                          mappedData.Qrcode.Count > 0 || mappedData.Barcode.Count > 0 ||
                                          mappedData.Image.Count > 0;
                        if (hasAnyData)
                        {
                            foreach (var (k, v) in mappedData.Replace)
                                processingData.Replace[k] = v;
                            foreach (var t in mappedData.Table)
                                processingData.Table.Add(t);
                            foreach (var (k, v) in mappedData.Qrcode)
                                processingData.Qrcode[k] = v;
                            foreach (var (k, v) in mappedData.Barcode)
                                processingData.Barcode[k] = v;
                            foreach (var (k, v) in mappedData.Image)
                                processingData.Image[k] = v;
                            mappingsApplied = true;
                        }
                    }
                }
            }
            catch (Exception mapEx)
            {
                _logger.LogWarning(mapEx, "Could not apply field mappings for template '{TemplateName}'", request.TemplateName);
            }

            if (!mappingsApplied)
                EnrichThaiTransforms(processingData);
            ApplySpacePadding(processingData.Replace);

            // Strategy Pattern: route to the correct ITemplateProcessor via EngineType
            var engineType = templateMeta?.EngineType ?? TemplateEngineType.OpenXML;
            if (engineType == TemplateEngineType.Univer)
                throw new NotSupportedException($"Template '{request.TemplateName}' ใช้ engine 'Univer' ที่ถูกยกเลิกแล้ว กรุณาอัปโหลดไฟล์ใหม่");
            var processor = _processors.Single(p => p.EngineType == engineType);
            var ctx = new TemplateProcessingContext(
                Template:     templateMeta ?? new TemplateMetadata { Format = extension, FileName = request.TemplateName ?? "" },
                TemplatePath: templatePath,
                Data:         processingData,
                OutputFormat: request.OutputFormat ?? ""
            );
            byte[] outputBytes = await processor.ProcessAsync(ctx);

            string outputExtension = string.Equals(request.OutputFormat, "pdf", StringComparison.OrdinalIgnoreCase) ? ".pdf" : extension;
            string contentType = outputExtension switch
            {
                ".pdf"  => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _       => "application/octet-stream"
            };

            long fileSizeBytes = outputBytes.Length;

            // Upload native file to MinIO
            string objectName = $"generated_{Guid.NewGuid()}{outputExtension}";
            using var outputStream = new MemoryStream(outputBytes);
            await _minioStorageService.UploadFileAsync(_minioSettings.DocumentBucket, objectName, outputStream, contentType);
            string downloadUrl = await _minioStorageService.GetPresignedUrlAsync(_minioSettings.DocumentBucket, objectName, expirySeconds: 3600);

            // Generate preview PDF via Gotenberg for non-PDF outputs
            string previewUrl = downloadUrl;
            string previewObjectName = objectName; // default: same as native (when output is already PDF)
            if (!string.Equals(outputExtension, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                byte[] previewBytes = await ConvertToPdfForPreviewAsync(outputBytes, objectName, outputExtension);
                previewObjectName = $"preview_{Guid.NewGuid()}.pdf";
                using var previewStream = new MemoryStream(previewBytes);
                await _minioStorageService.UploadFileAsync(_minioSettings.DocumentBucket, previewObjectName, previewStream, "application/pdf");
                previewUrl = await _minioStorageService.GetPresignedUrlAsync(_minioSettings.DocumentBucket, previewObjectName, expirySeconds: 3600);
            }

            stopwatch.Stop();
            log.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            log.Status          = "SUCCESS";
            log.OutputFileName  = objectName;
            log.FileSizeBytes   = fileSizeBytes;
            await _logService.RecordLogAsync(log);

            return new DocumentGenerationResponse(downloadUrl, previewUrl, log.Id, previewObjectName);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            log.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            log.Status = "FAILED";
            log.ErrorMessage = ex.Message;
            await _logService.RecordLogAsync(log);
            throw;
        }
    }

    /// <summary>
    /// Converts generated file bytes to PDF for iframe preview.
    /// HTML → Gotenberg Chromium (preserves web rendering).
    /// DOCX/XLSX and others → Gotenberg LibreOffice.
    /// </summary>
    private Task<byte[]> ConvertToPdfForPreviewAsync(byte[] bytes, string fileName, string extension)
    {
        if (string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase))
        {
            string html = System.Text.Encoding.UTF8.GetString(bytes);
            return _pdfConverter.ConvertHtmlToPdfAsync(html);
        }
        return _pdfConverter.ConvertToPdfAsync(bytes, fileName);
    }

    private DocumentProcessingData ConvertToDocumentProcessingData(DocumentGenerationRequest request)
    {
        if (request.Payload != null)
        {
            return request.Payload;
        }

        var result = new DocumentProcessingData();
        if (request.Data == null || request.Data.Count == 0)
        {
            return result;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // ตรวจสอบว่าใน Data มีคีย์พิเศษของ Qorstack ไหม เช่น "replace", "table", "qrcode", "qr", "image", "barcode"
        bool hasStructured = request.Data.ContainsKey("replace") || request.Data.ContainsKey("table") || 
                             request.Data.ContainsKey("qrcode") || request.Data.ContainsKey("qr") ||
                             request.Data.ContainsKey("image") || request.Data.ContainsKey("barcode");

        if (hasStructured)
        {
            try
            {
                if (request.Data.TryGetValue("replace", out var repVal) && repVal != null)
                {
                    if (repVal is JsonElement repElem)
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, string?>>(repElem.GetRawText(), options);
                        if (dict != null) result.Replace = dict;
                    }
                    else if (repVal is Dictionary<string, string?> dStr)
                    {
                        result.Replace = dStr;
                    }
                    else
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, string?>>(JsonSerializer.Serialize(repVal), options);
                        if (dict != null) result.Replace = dict;
                    }
                }

                if (request.Data.TryGetValue("table", out var tblVal) && tblVal != null)
                {
                    if (tblVal is JsonElement tblElem)
                    {
                        var tables = JsonSerializer.Deserialize<List<TableData>>(tblElem.GetRawText(), options);
                        if (tables != null) result.Table = tables;
                    }
                    else
                    {
                        var tables = JsonSerializer.Deserialize<List<TableData>>(JsonSerializer.Serialize(tblVal), options);
                        if (tables != null) result.Table = tables;
                    }
                }

                object? qrVal = null;
                if (request.Data.TryGetValue("qrcode", out var qrc)) qrVal = qrc;
                else if (request.Data.TryGetValue("qr", out var qrAlias)) qrVal = qrAlias;

                if (qrVal != null)
                {
                    if (qrVal is JsonElement qrElem)
                    {
                        var qrDict = JsonSerializer.Deserialize<Dictionary<string, QrCodeData?>>(qrElem.GetRawText(), options);
                        if (qrDict != null) result.Qrcode = qrDict;
                    }
                    else
                    {
                        var qrDict = JsonSerializer.Deserialize<Dictionary<string, QrCodeData?>>(JsonSerializer.Serialize(qrVal), options);
                        if (qrDict != null) result.Qrcode = qrDict;
                    }
                }

                if (request.Data.TryGetValue("barcode", out var bcVal) && bcVal != null)
                {
                    if (bcVal is JsonElement bcElem)
                    {
                        var bcDict = JsonSerializer.Deserialize<Dictionary<string, BarcodeData?>>(bcElem.GetRawText(), options);
                        if (bcDict != null) result.Barcode = bcDict;
                    }
                    else
                    {
                        var bcDict = JsonSerializer.Deserialize<Dictionary<string, BarcodeData?>>(JsonSerializer.Serialize(bcVal), options);
                        if (bcDict != null) result.Barcode = bcDict;
                    }
                }

                if (request.Data.TryGetValue("image", out var imgVal) && imgVal != null)
                {
                    if (imgVal is JsonElement imgElem)
                    {
                        var imgDict = JsonSerializer.Deserialize<Dictionary<string, ImageData?>>(imgElem.GetRawText(), options);
                        if (imgDict != null) result.Image = imgDict;
                    }
                    else
                    {
                        var imgDict = JsonSerializer.Deserialize<Dictionary<string, ImageData?>>(JsonSerializer.Serialize(imgVal), options);
                        if (imgDict != null) result.Image = imgDict;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize structured DocumentProcessingData, falling back to simple mapping");
            }
        }

        // แม็ปปิ้งแบบ Simple Variable & Auto-Detect QR/Barcode/Table
        foreach (var kvp in request.Data)
        {
            string key = kvp.Key.Trim();

            // Check if key is QR or Barcode
            bool isQr = key.StartsWith("qr:", StringComparison.OrdinalIgnoreCase) ||
                        key.StartsWith("@qr:", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("qr", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("qrcode", StringComparison.OrdinalIgnoreCase);

            bool isBarcode = key.StartsWith("barcode:", StringComparison.OrdinalIgnoreCase) || 
                             key.StartsWith("@bc:", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("barcode", StringComparison.OrdinalIgnoreCase) ||
                             key.IndexOf("barcode", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isQr && kvp.Value != null)
            {
                string qrKey = key.Replace("@qr:", "").Replace("qr:", "").Replace("@", "").Trim();
                string qrText = kvp.Value is JsonElement qe && qe.ValueKind == JsonValueKind.String ? qe.GetString() ?? "" : kvp.Value.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(qrText))
                {
                    result.Qrcode[qrKey] = new QrCodeData { Text = qrText };
                    result.Qrcode[key] = new QrCodeData { Text = qrText };
                }
            }
            else if (isBarcode && kvp.Value != null)
            {
                string bcKey = key.Replace("@bc:", "").Replace("barcode:", "").Replace("@", "").Trim();
                string bcText = kvp.Value is JsonElement be && be.ValueKind == JsonValueKind.String ? be.GetString() ?? "" : kvp.Value.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(bcText))
                {
                    result.Barcode[bcKey] = new BarcodeData { Text = bcText, Format = "Code128" };
                    result.Barcode[key] = new BarcodeData { Text = bcText, Format = "Code128" };
                }
            }

            // Check if value is array of rows (for table)
            if (kvp.Value is JsonElement arrElem && arrElem.ValueKind == JsonValueKind.Array)
            {
                try
                {
                    var rowsList = new List<Dictionary<string, object?>>();
                    foreach (var item in arrElem.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            var rowDict = new Dictionary<string, object?>();
                            foreach (var prop in item.EnumerateObject())
                            {
                                rowDict[prop.Name] = prop.Value.ValueKind switch
                                {
                                    JsonValueKind.String => prop.Value.GetString(),
                                    JsonValueKind.Number => prop.Value.GetRawText(),
                                    JsonValueKind.True => true,
                                    JsonValueKind.False => false,
                                    _ => prop.Value.ToString()
                                };
                            }
                            rowsList.Add(rowDict);
                        }
                    }
                    if (rowsList.Count > 0)
                    {
                        result.Table.Add(new TableData { Rows = rowsList });
                    }
                }
                catch { /* fallback to string */ }
            }

            string val = kvp.Value switch
            {
                JsonElement element => element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString() ?? string.Empty,
                    JsonValueKind.Number => element.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => element.ToString()
                },
                _ => kvp.Value?.ToString() ?? string.Empty
            };

            result.Replace[key] = val;
        }

        return result;
    }

    private static void ApplySpacePadding(Dictionary<string, string?> replace)
    {
        var toAdd = new Dictionary<string, string?>();
        foreach (var (key, val) in replace)
        {
            if (key.StartsWith(' ') || key.EndsWith(' ')) continue;
            toAdd.TryAdd($" {key} ", val);
            toAdd.TryAdd($" {key}", val);
            toAdd.TryAdd($"{key} ", val);
        }
        foreach (var (k, v) in toAdd)
            replace.TryAdd(k, v);
    }

    private static void EnrichThaiTransforms(DocumentProcessingData result)
    {
        if (result.Replace == null || result.Replace.Count == 0) return;

        var derived = new Dictionary<string, string?>();
        foreach (var (key, val) in result.Replace)
        {
            if (string.IsNullOrWhiteSpace(val) || val == "null" || key.Contains(':') || key.Contains('_')) continue;

            // Number check (Baht text & Currency)
            string cleaned = val.Replace(",", "").Replace("฿", "").Trim();
            if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal num))
            {
                string baht = ThaiDataTransformer.ToThaiBahtText(num);
                string currency = ThaiDataTransformer.FormatCurrency(val, 2);
                derived[$"{key}:baht"] = baht;
                derived[$"{key}_baht"] = baht;
                derived[$"{key}:currency"] = currency;
                derived[$"{key}_currency"] = currency;
            }

            // Date check (Thai Buddhist Era date)
            if ((val.Contains('-') || val.Contains('/')) &&
                (DateTime.TryParse(val, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _) ||
                 DateTime.TryParse(val, out _)))
            {
                string thaidate = ThaiDataTransformer.FormatThaiDate(val, false);
                string thaidateShort = ThaiDataTransformer.FormatThaiDate(val, true);
                derived[$"{key}:thaidate"] = thaidate;
                derived[$"{key}_thaidate"] = thaidate;
                derived[$"{key}:thaidate_short"] = thaidateShort;
                derived[$"{key}_thaidate_short"] = thaidateShort;
            }

            // Phone check
            string digits = System.Text.RegularExpressions.Regex.Replace(val, @"\D", "");
            if (digits.Length == 10 || digits.Length == 9)
            {
                string phone = ThaiDataTransformer.FormatPhone(val);
                derived[$"{key}:phone"] = phone;
                derived[$"{key}_phone"] = phone;
            }

            // Thai Citizen ID Card (13 digits)
            if (digits.Length == 13)
            {
                string idCard = ThaiDataTransformer.FormatThaiIdCard(val);
                derived[$"{key}:idcard"] = idCard;
                derived[$"{key}_idcard"] = idCard;
            }
        }

        foreach (var (k, v) in derived)
        {
            if (!result.Replace.ContainsKey(k))
            {
                result.Replace[k] = v;
            }
        }
    }

    /// <summary>
    /// Generates a document preview and returns raw bytes directly.
    /// RULE: Must NEVER upload to MinIO, write to GenerationLog, or create TemplateVersion entries.
    /// </summary>
    public async Task<byte[]> PreviewDocumentAsync(DocumentGenerationRequest request)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var project = httpContext?.Items["Project"] as Project;

        if (string.IsNullOrWhiteSpace(request.TemplateName))
            throw new ArgumentException("TemplateName is required.", nameof(request.TemplateName));

        string? templatePath = await _templateResolver.ResolveTemplatePathAsync(request.TemplateName, project?.Id);
        if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
            throw new FileNotFoundException($"Template '{request.TemplateName}' not found.");

        string extension = Path.GetExtension(templatePath).ToLower();
        var processingData = ConvertToDocumentProcessingData(request);

        // Resolve template metadata (needed for EngineType routing + field mappings)
        TemplateMetadata? templateMeta = null;
        bool mappingsApplied = false;
        try
        {
            templateMeta = await _templateResolver.GetMetadataAsync(request.TemplateName, project?.Id);
            if (templateMeta != null)
            {
                using var doc = JsonDocument.Parse(JsonSerializer.Serialize(request.Data));
                var mappedData = await _fieldMappingService.TryApplyMappingAsync(templateMeta.Id, doc.RootElement);
                if (mappedData != null)
                {
                    bool hasAnyData = mappedData.Replace.Count > 0 || mappedData.Table.Count > 0 ||
                                      mappedData.Qrcode.Count > 0 || mappedData.Barcode.Count > 0 ||
                                      mappedData.Image.Count > 0;
                    if (hasAnyData)
                    {
                        foreach (var (k, v) in mappedData.Replace)
                            processingData.Replace[k] = v;
                        foreach (var t in mappedData.Table)
                            processingData.Table.Add(t);
                        foreach (var (k, v) in mappedData.Qrcode)
                            processingData.Qrcode[k] = v;
                        foreach (var (k, v) in mappedData.Barcode)
                            processingData.Barcode[k] = v;
                        foreach (var (k, v) in mappedData.Image)
                            processingData.Image[k] = v;
                        mappingsApplied = true;
                    }
                }
            }
        }
        catch (Exception mapEx)
        {
            _logger.LogWarning(mapEx, "[Preview] Could not apply field mappings for '{TemplateName}'", request.TemplateName);
        }

        // AP-006: EnrichThaiTransforms runs only when mappings were NOT applied
        if (!mappingsApplied)
            EnrichThaiTransforms(processingData);
        ApplySpacePadding(processingData.Replace);

        // Strategy Pattern: always output PDF for preview (IsPreview=true)
        var engineType = templateMeta?.EngineType ?? TemplateEngineType.OpenXML;
        if (engineType == TemplateEngineType.Univer)
            throw new NotSupportedException($"Template '{request.TemplateName}' ใช้ engine 'Univer' ที่ถูกยกเลิกแล้ว กรุณาอัปโหลดไฟล์ใหม่");
        var processor = _processors.Single(p => p.EngineType == engineType);
        var ctx = new TemplateProcessingContext(
            Template:     templateMeta ?? new TemplateMetadata { Format = extension, FileName = request.TemplateName ?? "" },
            TemplatePath: templatePath,
            Data:         processingData,
            OutputFormat: "pdf",
            IsPreview:    true
        );
        byte[] pdfBytes = await processor.ProcessAsync(ctx);

        _logger.LogInformation("[Preview] Generated preview PDF for '{TemplateName}' ({Bytes} bytes) — NO MinIO upload.",
            request.TemplateName, pdfBytes.Length);

        return pdfBytes;
    }
}
