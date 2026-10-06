using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Enums;

public class GenerationStatus : Enumeration
{
    public static readonly GenerationStatus Success = new(1, "SUCCESS");
    public static readonly GenerationStatus Failed = new(2, "FAILED");
    public static readonly GenerationStatus Processing = new(3, "PROCESSING");
    public static readonly GenerationStatus Timeout = new(4, "TIMEOUT");
    public static readonly GenerationStatus ValidationFailed = new(5, "VALIDATION_FAILED");

    private GenerationStatus(int id, string name) : base(id, name) { }

    public static GenerationStatus FromName(string name) => FromDisplayName<GenerationStatus>(name);

    public static implicit operator string(GenerationStatus status) => status.Name;
}
