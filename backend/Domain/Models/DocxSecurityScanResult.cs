namespace SmkDocServer.Domain.Models;

public class DocxSecurityScanResult
{
    public bool IsSafe { get; set; }
    public List<string> Threats { get; set; } = new();
}

