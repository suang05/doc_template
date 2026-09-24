using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmkDocServer.Domain.Entities;
using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Domain.Models;
using SmkDocServer.Infrastructure.Data;

namespace SmkDocServer.Application.Services;

public class FieldMappingDto
{
    public Guid? Id { get; set; }
    public string Placeholder { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string PlaceholderType { get; set; } = "text";
    public string? Transform { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    /// <summary>"json" | "sql" | "expr"</summary>
    public string DataSourceType { get; set; } = "json";
    public Guid? DataConnectionId { get; set; }
    public string? SqlQuery { get; set; }
}

public class FieldMappingPreviewResult
{
    public string Placeholder { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string? RawValue { get; set; }
    public string? TransformedValue { get; set; }
    public string? Transform { get; set; }
    public bool Found { get; set; }
    public string DataSourceType { get; set; } = "json";
}

public class FieldMappingService
{
    private readonly AppDbContext _dbContext;
    private readonly IDataConnectionService _dataConnService;
    private readonly ILogger<FieldMappingService> _logger;

    public FieldMappingService(
        AppDbContext dbContext,
        IDataConnectionService dataConnService,
        ILogger<FieldMappingService> logger)
    {
        _dbContext = dbContext;
        _dataConnService = dataConnService;
        _logger = logger;
    }

    public async Task<List<FieldMappingDto>> GetMappingsAsync(Guid templateId)
    {
        return await _dbContext.FieldMappings
            .AsNoTracking()
            .Where(m => m.TemplateId == templateId)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Placeholder)
            .Select(m => new FieldMappingDto
            {
                Id = m.Id,
                Placeholder = m.Placeholder,
                SourcePath = m.SourcePath,
                Label = m.Label,
                PlaceholderType = m.PlaceholderType,
                Transform = m.Transform,
                DefaultValue = m.DefaultValue,
                IsRequired = m.IsRequired,
                SortOrder = m.SortOrder,
                DataSourceType = m.DataSourceType,
                DataConnectionId = m.DataConnectionId,
                SqlQuery = m.SqlQuery
            })
            .ToListAsync();
    }

    public async Task<List<FieldMappingDto>> SaveMappingsAsync(Guid templateId, List<FieldMappingDto> mappings)
    {
        var existing = await _dbContext.FieldMappings
            .Where(m => m.TemplateId == templateId)
            .ToListAsync();

        _dbContext.FieldMappings.RemoveRange(existing);

        int order = 0;
        var newEntities = mappings.Select(dto => new FieldMapping
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            Placeholder = dto.Placeholder.Trim(),
            SourcePath = dto.SourcePath?.Trim() ?? string.Empty,
            Label = dto.Label?.Trim() ?? string.Empty,
            PlaceholderType = string.IsNullOrWhiteSpace(dto.PlaceholderType) ? "text" : dto.PlaceholderType.Trim().ToLower(),
            Transform = string.IsNullOrWhiteSpace(dto.Transform) ? null : dto.Transform.Trim(),
            DefaultValue = dto.DefaultValue,
            IsRequired = dto.IsRequired,
            SortOrder = order++,
            DataSourceType = string.IsNullOrWhiteSpace(dto.DataSourceType) ? "json" : dto.DataSourceType.Trim().ToLower(),
            DataConnectionId = dto.DataConnectionId,
            SqlQuery = string.IsNullOrWhiteSpace(dto.SqlQuery) ? null : dto.SqlQuery.Trim(),
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        await _dbContext.FieldMappings.AddRangeAsync(newEntities);
        await _dbContext.SaveChangesAsync();

        return await GetMappingsAsync(templateId);
    }

    /// <summary>
    /// Previews mapping resolution and transformations against a sample JSON payload.
    /// expr-type mappings are evaluated using already-resolved placeholders in this preview.
    /// sql-type mappings cannot execute at preview time — they show a placeholder note.
    /// </summary>
    public List<FieldMappingPreviewResult> PreviewMappings(List<FieldMappingDto> mappings, JsonElement sampleJson)
    {
        var results = new List<FieldMappingPreviewResult>();

        foreach (var mapping in mappings)
        {
            string dataSource = mapping.DataSourceType?.ToLower() ?? "json";

            // expr — evaluate using previously resolved Replace values in this preview list
            if (dataSource == "expr")
            {
                var resolvedVars = results
                    .Where(r => r.TransformedValue != null)
                    .ToDictionary(r => r.Placeholder, r => r.TransformedValue);
                string exprResult = MathExpressionResolver.Evaluate(mapping.SourcePath, resolvedVars);
                results.Add(new FieldMappingPreviewResult
                {
                    Placeholder = mapping.Placeholder,
                    SourcePath = mapping.SourcePath,
                    RawValue = mapping.SourcePath,
                    TransformedValue = exprResult,
                    Transform = mapping.Transform,
                    Found = !string.IsNullOrEmpty(exprResult),
                    DataSourceType = "expr"
                });
                continue;
            }

            // sql — cannot execute external DB at preview time
            if (dataSource == "sql")
            {
                results.Add(new FieldMappingPreviewResult
                {
                    Placeholder = mapping.Placeholder,
                    SourcePath = mapping.SourcePath,
                    RawValue = null,
                    TransformedValue = mapping.DefaultValue ?? "(sql — ไม่สามารถดูตัวอย่างได้)",
                    Transform = mapping.Transform,
                    Found = false,
                    DataSourceType = "sql"
                });
                continue;
            }

            // json (default)
            string? rawValue = JsonPathResolver.ResolveValue(sampleJson, mapping.SourcePath);
            bool found = rawValue != null;
            string? effectiveValue = rawValue ?? mapping.DefaultValue;
            string? transformedValue = effectiveValue;

            if (!string.IsNullOrWhiteSpace(effectiveValue) && !string.IsNullOrWhiteSpace(mapping.Transform))
                transformedValue = ApplyTransform(effectiveValue, mapping.Transform);

            results.Add(new FieldMappingPreviewResult
            {
                Placeholder = mapping.Placeholder,
                SourcePath = mapping.SourcePath,
                RawValue = rawValue,
                TransformedValue = transformedValue,
                Transform = mapping.Transform,
                Found = found,
                DataSourceType = "json"
            });
        }

        return results;
    }

    /// <summary>
    /// Applies configured field mappings to populate DocumentProcessingData from a raw JSON payload.
    /// Supports DataSourceType: "json" (default), "expr" (math expression), "sql" (external DB query).
    /// </summary>
    public async Task<DocumentProcessingData?> TryApplyMappingAsync(Guid templateId, JsonElement rawJson)
    {
        var mappings = await _dbContext.FieldMappings
            .AsNoTracking()
            .Where(m => m.TemplateId == templateId)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();

        if (mappings.Count == 0) return null;

        var docData = new DocumentProcessingData();

        // SQL result cache: keyed by (DataConnectionId, SqlQuery) → column dict
        var sqlCache = new Dictionary<string, Dictionary<string, string?>>();

        foreach (var mapping in mappings)
        {
            string dataSource = mapping.DataSourceType?.ToLower() ?? "json";

            // ── expr ────────────────────────────────────────────────────────
            if (dataSource == "expr")
            {
                string result = MathExpressionResolver.Evaluate(
                    mapping.SourcePath,
                    docData.Replace);
                if (!string.IsNullOrWhiteSpace(result))
                    docData.Replace[mapping.Placeholder] = result;
                continue;
            }

            // ── sql ─────────────────────────────────────────────────────────
            if (dataSource == "sql" && mapping.DataConnectionId.HasValue && !string.IsNullOrWhiteSpace(mapping.SqlQuery))
            {
                string cacheKey = $"{mapping.DataConnectionId}::{mapping.SqlQuery}";
                if (!sqlCache.TryGetValue(cacheKey, out var row))
                {
                    row = await ExecuteSqlFirstRowAsync(mapping.DataConnectionId.Value, mapping.SqlQuery);
                    sqlCache[cacheKey] = row;
                }

                string? colVal = null;
                if (row != null && !string.IsNullOrWhiteSpace(mapping.SourcePath))
                    row.TryGetValue(mapping.SourcePath, out colVal);

                string? finalVal = colVal ?? mapping.DefaultValue;
                if (!string.IsNullOrWhiteSpace(finalVal) && !string.IsNullOrWhiteSpace(mapping.Transform))
                    finalVal = ApplyTransform(finalVal, mapping.Transform);
                if (finalVal != null)
                    docData.Replace[mapping.Placeholder] = finalVal;
                continue;
            }

            // ── json (default) ───────────────────────────────────────────────
            switch (mapping.PlaceholderType.ToLower())
            {
                case "table":
                {
                    var rows = JsonPathResolver.ResolveArray(rawJson, mapping.SourcePath);
                    if (rows != null && rows.Count > 0)
                    {
                        var tableData = new TableData
                        {
                            Rows = rows.Select(r =>
                                r.ToDictionary(kv => kv.Key, kv => (object?)kv.Value))
                                .ToList()
                        };
                        docData.Table.Add(tableData);
                    }
                    break;
                }

                case "qrcode":
                {
                    string? val = JsonPathResolver.ResolveValue(rawJson, mapping.SourcePath) ?? mapping.DefaultValue;
                    if (!string.IsNullOrWhiteSpace(val))
                        docData.Qrcode[mapping.Placeholder] = new QrCodeData { Text = val };
                    break;
                }

                case "barcode":
                {
                    string? val = JsonPathResolver.ResolveValue(rawJson, mapping.SourcePath) ?? mapping.DefaultValue;
                    if (!string.IsNullOrWhiteSpace(val))
                        docData.Barcode[mapping.Placeholder] = new BarcodeData { Text = val };
                    break;
                }

                case "image":
                {
                    string? val = JsonPathResolver.ResolveValue(rawJson, mapping.SourcePath) ?? mapping.DefaultValue;
                    if (!string.IsNullOrWhiteSpace(val))
                        docData.Image[mapping.Placeholder] = new ImageData { Src = val };
                    break;
                }

                default: // "text"
                {
                    string? rawValue = JsonPathResolver.ResolveValue(rawJson, mapping.SourcePath);
                    string? finalValue = rawValue ?? mapping.DefaultValue;
                    if (!string.IsNullOrWhiteSpace(finalValue) && !string.IsNullOrWhiteSpace(mapping.Transform))
                        finalValue = ApplyTransform(finalValue, mapping.Transform);
                    if (finalValue != null)
                        docData.Replace[mapping.Placeholder] = finalValue;
                    break;
                }
            }
        }

        return docData;
    }

    /// <summary>
    /// Executes a SQL SELECT and returns the first row as a column → value dictionary.
    /// Supports PostgreSQL only; other DatabaseTypes return null gracefully.
    /// </summary>
    private async Task<Dictionary<string, string?>> ExecuteSqlFirstRowAsync(Guid connectionId, string sql)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            string? cs = await _dataConnService.GetDecryptedConnectionStringAsync(connectionId);
            if (string.IsNullOrWhiteSpace(cs)) return result;

            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                    result[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQL DataSource query failed for connection {Id}.", connectionId);
        }
        return result;
    }

    public static string ApplyTransform(string value, string transformName)
    {
        return transformName.ToLowerInvariant() switch
        {
            "formatthaibaht" or "baht" or "thai_baht" => ThaiDataTransformer.ToThaiBahtText(value),
            "formatthaidate" or "thaidate" or "date_th" => ThaiDataTransformer.FormatThaiDate(value),
            "formatthaidatetime" or "thaidatetime" => ThaiDataTransformer.FormatThaiDateTime(value),
            "formatcurrency" or "currency" => ThaiDataTransformer.FormatCurrency(value),
            "formatphone" or "phone" => ThaiDataTransformer.FormatPhone(value),
            "formatthaiid" or "idcard" or "thai_id" => ThaiDataTransformer.FormatThaiIdCard(value),
            _ => value
        };
    }
}
