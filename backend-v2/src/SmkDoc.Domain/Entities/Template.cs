using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain aggregate root representing a document template.
/// Owns TemplateVersion, FieldMapping, and TemplateDataset child entities.
/// </summary>
public sealed class Template : BaseEntity, IMustHaveProject
{
    public Guid ProjectId { get; private set; }
    public TemplateName Name { get; private set; } = null!;
    public TemplateSlug Slug { get; private set; } = null!;
    public string? Category { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? CurrentVersionId { get; private set; }

    private readonly List<FieldMapping> _fieldMappings = new();
    private readonly List<TemplateVersion> _versions = new();
    private readonly List<TemplateDataset> _templateDatasets = new();

    // Child entity collections strictly encapsulated within aggregate root
    public IReadOnlyCollection<FieldMapping> FieldMappings => _fieldMappings.AsReadOnly();
    public IReadOnlyCollection<TemplateVersion> Versions => _versions.AsReadOnly();
    public IReadOnlyCollection<TemplateDataset> TemplateDatasets => _templateDatasets.AsReadOnly();

    // Navigation property for EF Core materialization of current version
    public TemplateVersion? CurrentVersion { get; private set; }

    // For EF Core materialization only
    private Template() { }

    internal Template(
        Guid? id,
        Guid projectId,
        TemplateName name,
        TemplateSlug slug,
        string? category,
        DateTimeOffset now)
        : base(id, createdAt: now)
    {
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
        Name = Guard.NotNull(name, nameof(Name));
        Slug = Guard.NotNull(slug, nameof(Slug));
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        IsActive = true;
    }

    /// <summary>
    /// Canonical factory method for creating a document template.
    /// Requires strongly-typed Value Objects and deterministic timestamp.
    /// </summary>
    public static Template Create(
        Guid projectId,
        TemplateName name,
        TemplateSlug slug,
        string? category,
        DateTimeOffset now) =>
        new(null, projectId, name, slug, category, now);

    public TemplateVersion CreateDraftVersion(
        int versionNumber,
        string storageKey,
        TemplateFormat? fileFormat,
        string? createdBy,
        DateTimeOffset now,
        string? commitMessage = null)
    {
        if (_versions.Any(v => v.Version == versionNumber))
        {
            throw new DuplicateVersionException(Id, versionNumber);
        }

        var version = TemplateVersion.Draft(Id, versionNumber, storageKey, fileFormat, createdBy, now, commitMessage);
        _versions.Add(version);
        SetUpdated(now);
        return version;
    }

    public void AddVersion(TemplateVersion version, DateTimeOffset now)
    {
        Guard.NotNull(version, nameof(version));

        if (version.TemplateId != Id)
        {
            throw new DomainValidationException($"Version template ID '{version.TemplateId}' does not match template ID '{Id}'.");
        }

        if (_versions.Any(v => v.Version == version.Version))
        {
            throw new DuplicateVersionException(Id, version.Version);
        }

        _versions.Add(version);
        SetUpdated(now);
    }

    public void SetCurrentVersion(Guid versionId, DateTimeOffset now)
    {
        Guard.NotEmpty(versionId, nameof(versionId));

        if (!IsActive)
        {
            throw new BusinessRuleViolationException(
                "Cannot assign a current version to an inactive template.",
                "INACTIVE_TEMPLATE");
        }

        if (_versions.Count > 0 && !_versions.Any(v => v.Id == versionId))
        {
            throw new VersionNotInTemplateException(Id, versionId);
        }

        CurrentVersionId = versionId;
        SetUpdated(now);
    }

    public void AddFieldMapping(FieldMapping mapping, DateTimeOffset now)
    {
        Guard.NotNull(mapping, nameof(mapping));

        if (mapping.TemplateId != Id)
        {
            throw new DomainValidationException($"FieldMapping template ID '{mapping.TemplateId}' does not match template ID '{Id}'.");
        }

        if (_fieldMappings.Any(m => string.Equals(m.Placeholder, mapping.Placeholder, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DuplicatePlaceholderException(Id, mapping.Placeholder);
        }

        _fieldMappings.Add(mapping);
        SetUpdated(now);
    }

    public void ReplaceFieldMappings(IEnumerable<FieldMapping> mappings, DateTimeOffset now)
    {
        Guard.NotNull(mappings, nameof(mappings));

        var mappingList = mappings.ToList();
        foreach (var m in mappingList)
        {
            Guard.NotNull(m, nameof(FieldMapping));

            if (m.TemplateId != Id)
            {
                throw new DomainValidationException($"FieldMapping template ID '{m.TemplateId}' does not match template ID '{Id}'.");
            }
        }

        var duplicates = mappingList
            .GroupBy(m => m.Placeholder, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new DuplicatePlaceholderException(Id, string.Join(", ", duplicates));
        }

        _fieldMappings.Clear();
        _fieldMappings.AddRange(mappingList);
        SetUpdated(now);
    }

    public void AttachDataset(Guid datasetId, DatasetAlias alias, int sortOrder, DateTimeOffset now)
    {
        Guard.NotEmpty(datasetId, nameof(datasetId));
        Guard.NotNull(alias, nameof(alias));

        if (_templateDatasets.Any(d => d.Alias == alias))
        {
            throw new DuplicateDatasetAliasException(Id, alias.Value);
        }

        _templateDatasets.Add(TemplateDataset.Create(Id, datasetId, alias, sortOrder, now));
        SetUpdated(now);
    }

    public void DetachDataset(Guid datasetId, DateTimeOffset now)
    {
        var existing = _templateDatasets.FirstOrDefault(d => d.DatasetId == datasetId);
        if (existing != null)
        {
            _templateDatasets.Remove(existing);
            SetUpdated(now);
        }
    }

    public void ReplaceDatasets(IEnumerable<TemplateDataset> datasets, DateTimeOffset now)
    {
        Guard.NotNull(datasets, nameof(datasets));

        var datasetList = datasets.ToList();
        foreach (var d in datasetList)
        {
            Guard.NotNull(d, nameof(TemplateDataset));

            if (d.TemplateId != Id)
            {
                throw new DomainValidationException($"TemplateDataset template ID '{d.TemplateId}' does not match template ID '{Id}'.");
            }
        }

        var duplicateAliases = datasetList
            .GroupBy(d => d.Alias.Value, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateAliases.Count > 0)
        {
            throw new DuplicateDatasetAliasException(Id, string.Join(", ", duplicateAliases));
        }

        _templateDatasets.Clear();
        _templateDatasets.AddRange(datasetList);
        SetUpdated(now);
    }

    public void UpdateDetails(TemplateName name, string? category, DateTimeOffset now)
    {
        Name = Guard.NotNull(name, nameof(Name));
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        SetUpdated(now);
    }

    public void Activate(DateTimeOffset now)
    {
        if (IsActive) return;
        IsActive = true;
        SetUpdated(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false;
        SetUpdated(now);
    }
}

