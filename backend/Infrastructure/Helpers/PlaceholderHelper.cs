using System.Text.RegularExpressions;

namespace SmkDocServer.Infrastructure.Helpers;

/// <summary>
/// SSoT for the {{...}} placeholder regex — Rule 11 (DRY).
/// All template processors (HTML, ReportBro, etc.) must use Pattern here instead of defining their own.
/// Matches: {{variable}} and {{variable:transform}} — does NOT match loop tags {{#tag}} or {{/tag}}.
/// </summary>
public static class PlaceholderHelper
{
    /// <summary>
    /// Compiled regex matching {{name}} or {{name:transform}}.
    /// Group 1 = full token (e.g. "date:thai"), Group 2 = transform suffix if present (e.g. "thai").
    /// </summary>
    public static readonly Regex Pattern =
        new(@"\{\{(\w+(?::\w+)*)\}\}", RegexOptions.Compiled);
}
