namespace SmkDocServer.Domain.Models;

/// <summary>
/// Defines which rendering engine processes a given template.
/// Used to route documents to the correct ITemplateProcessor implementation.
/// </summary>
public enum TemplateEngineType
{
    /// <summary>Upload .docx / .xlsx — processed by OpenXML (Qorstack)</summary>
    OpenXML = 0,

    /// <summary>Web-authored HTML — processed by Tiptap + Gotenberg Chromium</summary>
    Tiptap = 1,

    /// <summary>Removed in Sprint 6-7. Value kept for DB compatibility; no processor registered — templates with this type will be rejected at generation time.</summary>
    Univer = 2,

    /// <summary>Pixel-perfect JSON layout — processed by ReportBro Python server</summary>
    ReportBro = 3
}
