using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Domain.Models;

namespace SmkDocServer.Infrastructure.Services.Document;

/// <summary>
/// ITemplateProcessor for ReportBro (EngineType.ReportBro = 3).
/// Flow: load .json layout from disk → PUT to reportbro-server → poll GET until ready → return PDF bytes.
/// </summary>
public class ReportBroProcessor : ITemplateProcessor
{
    private readonly HttpClient _http;
    private readonly ReportBroSettings _settings;
    private readonly ILogger<ReportBroProcessor> _logger;

    public TemplateEngineType EngineType => TemplateEngineType.ReportBro;

    public ReportBroProcessor(
        IHttpClientFactory httpClientFactory,
        IOptions<ReportBroSettings> settings,
        ILogger<ReportBroProcessor> logger)
    {
        _http = httpClientFactory.CreateClient();
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<byte[]> ProcessAsync(TemplateProcessingContext ctx, CancellationToken cancellationToken = default)
    {
        // 1. Load JSON report definition from local file path
        if (!File.Exists(ctx.TemplatePath))
            throw new FileNotFoundException($"ReportBro layout not found: {ctx.TemplatePath}");

        string reportDefinitionJson = await File.ReadAllTextAsync(ctx.TemplatePath, cancellationToken);
        JsonElement reportDefinition;
        try
        {
            reportDefinition = JsonSerializer.Deserialize<JsonElement>(reportDefinitionJson);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Invalid ReportBro JSON layout: {ex.Message}", ex);
        }

        // Flatten data: merge Replace fields + include structured data for ReportBro
        var dataDict = ctx.Data.Replace?
            .ToDictionary(kv => kv.Key, kv => (object?)kv.Value)
            ?? new Dictionary<string, object?>();
        if (ctx.Data.Table?.Count > 0)
            dataDict["_tables"] = ctx.Data.Table;

        // 2. PUT /api/report — submit generation request
        var payload = new
        {
            report = reportDefinition,
            data = dataDict,
            isTestData = false,
            outputFormat = "pdf"
        };

        string putUrl = $"{_settings.BaseUrl.TrimEnd('/')}/api/report";
        HttpResponseMessage putResponse;
        try
        {
            putResponse = await _http.PutAsJsonAsync(putUrl, payload, cancellationToken);
            putResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit report to ReportBro server at {Url}", putUrl);
            throw new InvalidOperationException($"ReportBro server error: {ex.Message}", ex);
        }

        var putResult = await putResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!putResult.TryGetProperty("key", out JsonElement keyElement))
            throw new InvalidOperationException("ReportBro server did not return a report key.");

        string reportKey = keyElement.GetString()
            ?? throw new InvalidOperationException("ReportBro server returned an empty report key.");

        // 3. Poll GET /api/report/{key}?outputFormat=pdf until done or timeout
        string getUrl = $"{_settings.BaseUrl.TrimEnd('/')}/api/report/{reportKey}?outputFormat=pdf";
        var deadline = DateTime.UtcNow.AddSeconds(_settings.TimeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HttpResponseMessage getResponse = await _http.GetAsync(getUrl, cancellationToken);

            if (getResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string contentType = getResponse.Content.Headers.ContentType?.MediaType ?? "";

                if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) ||
                    contentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase))
                {
                    return await getResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                }

                // Server returned JSON (still processing or error)
                var statusJson = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                if (statusJson.TryGetProperty("status", out var statusProp))
                {
                    string status = statusProp.GetString() ?? "";
                    if (status.Equals("error", StringComparison.OrdinalIgnoreCase))
                    {
                        string detail = statusJson.TryGetProperty("errorMessage", out var errProp)
                            ? errProp.GetString() ?? "unknown error" : "unknown error";
                        throw new InvalidOperationException($"ReportBro generation failed: {detail}");
                    }
                }
            }
            else if (getResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"ReportBro report key '{reportKey}' not found on server.");
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException($"ReportBro did not finish within {_settings.TimeoutSeconds}s for key '{reportKey}'.");
    }
}
