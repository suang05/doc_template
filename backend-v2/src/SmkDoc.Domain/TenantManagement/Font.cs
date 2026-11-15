using SmkDoc.Domain.Common;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.TenantManagement;

/// <summary>
/// Registered font asset within the platform catalog (global when ProjectId is null, or optional tenant-scoped font under coexistence).
/// </summary>
public sealed class Font : AggregateRoot<FontId>
{
    public const int MaxFamilyNameLength = 120;
    public const int MaxWeightLength = 32;
    public const int MaxStyleLength = 32;
    public const int MaxFormatLength = 16;
    public const int MaxStorageKeyLength = 512;

    public string FamilyName { get; private set; } = string.Empty;
    public string Weight { get; private set; } = "normal";
    public string Style { get; private set; } = "normal";
    public string Format { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public Sha256Hash ChecksumSha256 { get; private set; } = null!;
    public bool IsSystemFont { get; private set; }
    public ProjectId? ProjectId { get; private set; }

    // For EF Core materialization only
    private Font() { }

    private Font(
        FontId id,
        string familyName,
        string weight,
        string style,
        string format,
        string storageKey,
        Sha256Hash checksumSha256,
        bool isSystemFont,
        ProjectId? projectId,
        DateTimeOffset now)
        : base(id, now)
    {
        FamilyName = Guard.MaxLength(Guard.NotBlank(familyName, nameof(FamilyName)), nameof(FamilyName), MaxFamilyNameLength);
        Weight = Guard.MaxLength(string.IsNullOrWhiteSpace(weight) ? "normal" : weight.Trim(), nameof(Weight), MaxWeightLength);
        Style = Guard.MaxLength(string.IsNullOrWhiteSpace(style) ? "normal" : style.Trim(), nameof(Style), MaxStyleLength);
        Format = Guard.MaxLength(Guard.NotBlank(format, nameof(Format)), nameof(Format), MaxFormatLength);
        StorageKey = Guard.MaxLength(Guard.NotBlank(storageKey, nameof(StorageKey)), nameof(StorageKey), MaxStorageKeyLength);
        ChecksumSha256 = Guard.NotNull(checksumSha256, nameof(ChecksumSha256));
        IsSystemFont = isSystemFont;
        ProjectId = projectId is { IsEmpty: false } pid ? pid : null;
    }

    public static Font Register(
        string familyName,
        string weight,
        string style,
        string format,
        string storageKey,
        Sha256Hash checksumSha256,
        bool isSystemFont,
        ProjectId? projectId,
        DateTimeOffset now) =>
        new(
            FontId.New(),
            familyName,
            weight,
            style,
            format,
            storageKey,
            checksumSha256,
            isSystemFont,
            projectId,
            now);

    public void UpdateMetadata(string familyName, string weight, string style, DateTimeOffset now)
    {
        var normalizedFamily = Guard.MaxLength(Guard.NotBlank(familyName, nameof(FamilyName)), nameof(FamilyName), MaxFamilyNameLength);
        var normalizedWeight = Guard.MaxLength(string.IsNullOrWhiteSpace(weight) ? "normal" : weight.Trim(), nameof(Weight), MaxWeightLength);
        var normalizedStyle = Guard.MaxLength(string.IsNullOrWhiteSpace(style) ? "normal" : style.Trim(), nameof(Style), MaxStyleLength);

        if (FamilyName == normalizedFamily && Weight == normalizedWeight && Style == normalizedStyle)
        {
            return;
        }

        FamilyName = normalizedFamily;
        Weight = normalizedWeight;
        Style = normalizedStyle;
        SetUpdated(now);
    }
}
