using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SmkDocServer.Domain.Models;

public class DocumentGenerationRequest
{
    [Required]
    public string TemplateName { get; set; } = string.Empty;

    // รองรับทั้งแบบเก่า {"username": "value"} และแบบใหม่ Qorstack {"replace": {...}, "tables": [...], ...}
    public Dictionary<string, object>? Data { get; set; }

    [JsonPropertyName("payload")]
    public DocumentProcessingData? Payload { get; set; }

    // กำหนดว่าอยากได้ผลลัพธ์เป็นไฟล์อะไร: "pdf", "docx", "xlsx", หรือ "original"
    public string OutputFormat { get; set; } = "pdf"; 
}
