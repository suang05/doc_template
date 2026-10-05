using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain aggregate root representing a document template.
/// </summary>
public class Template : BaseEntity, IMustHaveProject
{
    public string Name { get; private set; } = string.Empty;
    public TemplateSlug Slug { get; private set; } = null!;
    public string? Category { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid? CurrentVersionId { get; private set; }
    public Guid ProjectId { get; private set; }

    private readonly List<FieldMapping> _fieldMappings = new();
    private readonly List<TemplateVersion> _versions = new();
    private readonly List<TemplateDataset> _templateDatasets = new();

    // Navigation properties
    public virtual Project? Project { get; private set; }
    public virtual TemplateVersion? CurrentVersion { get; private set; }
    public virtual IReadOnlyCollection<FieldMapping> FieldMappings => _fieldMappings.AsReadOnly();
    public virtual IReadOnlyCollection<TemplateVersion> Versions => _versions.AsReadOnly();
    public virtual IReadOnlyCollection<TemplateDataset> TemplateDatasets => _templateDatasets.AsReadOnly();

    // For EF Core materialization
    private Template() { }

    public Template(Guid projectId, string name, TemplateSlug slug, string? category = null, Guid? id = null)
        : base(id)
    {
        if (projectId == Guid.Empty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Template name cannot be empty or whitespace.");
        }

        if (slug == null)
        {
            throw new DomainValidationException("Template slug cannot be null.");
        }

        ProjectId = projectId;
        Name = name.Trim();
        Slug = slug;
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        IsActive = true;
    }

    public Template(Guid projectId, string name, string slug, string? category = null, Guid? id = null)
        : this(projectId, name, new TemplateSlug(slug), category, id)
    {
    }

    public void AddVersion(TemplateVersion version)
    {
        if (version == null)
        {
            throw new DomainValidationException("TemplateVersion cannot be null.");
        }

        if (version.TemplateId != Id)
        {
            throw new DomainValidationException($"Version template ID '{version.TemplateId}' does not match template ID '{Id}'.");
        }

        if (_versions.Any(v => v.Version == version.Version))
        {
            throw new BusinessRuleViolationException(
                $"Version {version.Version} already exists in template '{Id}'.",
                "DUPLICATE_VERSION");
        }

        _versions.Add(version);
        SetUpdated();
    }

    public void SetCurrentVersion(Guid versionId)
    {
        if (versionId == Guid.Empty)
        {
            throw new DomainValidationException("VersionId cannot be empty.");
        }

        if (!IsActive)
        {
            throw new BusinessRuleViolationException(
                "Cannot assign a current version to an inactive template.", 
                "INACTIVE_TEMPLATE");
        }

        if (_versions.Count > 0 && !_versions.Any(v => v.Id == versionId))
        {
            throw new BusinessRuleViolationException(
                $"Version '{versionId}' does not belong to template '{Id}'.",
                "VERSION_NOT_IN_TEMPLATE");
        }

        CurrentVersionId = versionId;
        SetUpdated();
    }

    public void AddFieldMapping(FieldMapping mapping)
    {
        if (mapping == null)
        {
            throw new DomainValidationException("FieldMapping cannot be null.");
        }

        if (mapping.TemplateId != Id)
        {
            throw new DomainValidationException($"FieldMapping template ID '{mapping.TemplateId}' does not match template ID '{Id}'.");
        }

        if (_fieldMappings.Any(m => string.Equals(m.Placeholder, mapping.Placeholder, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessRuleViolationException(
                $"FieldMapping with placeholder '{mapping.Placeholder}' already exists in template '{Id}'.",
                "DUPLICATE_PLACEHOLDER");
        }

        _fieldMappings.Add(mapping);
        SetUpdated();
    }

    public void ReplaceFieldMappings(IEnumerable<FieldMapping> mappings)
    {
        if (mappings == null)
        {
            throw new DomainValidationException("Mappings collection cannot be null.");
        }

        var mappingList = mappings.ToList();
        foreach (var m in mappingList)
        {
            if (m == null)
            {
                throw new DomainValidationException("FieldMapping item cannot be null.");
            }

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
            throw new BusinessRuleViolationException(
                $"Duplicate placeholders detected in template '{Id}': {string.Join(", ", duplicates)}.",
                "DUPLICATE_PLACEHOLDER");
        }

        _fieldMappings.Clear();
        _fieldMappings.AddRange(mappingList);
        SetUpdated();
    }

    public void AttachDataset(Guid datasetId, string alias, int sortOrder)
    {
        if (datasetId == Guid.Empty)
        {
            throw new DomainValidationException("DatasetId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new DomainValidationException("Dataset alias cannot be empty or whitespace.");
        }

        var normalizedAlias = alias.Trim().ToLowerInvariant();
        if (_templateDatasets.Any(d => string.Equals(d.Alias, normalizedAlias, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessRuleViolationException(
                $"Dataset alias '{normalizedAlias}' already assigned to template '{Id}'.",
                "DUPLICATE_DATASET_ALIAS");
        }

        _templateDatasets.Add(new TemplateDataset(Id, datasetId, normalizedAlias, sortOrder));
        SetUpdated();
    }

    public void DetachDataset(Guid datasetId)
    {
        var existing = _templateDatasets.FirstOrDefault(d => d.DatasetId == datasetId);
        if (existing != null)
        {
            _templateDatasets.Remove(existing);
            SetUpdated();
        }
    }

    public void ReplaceDatasets(IEnumerable<TemplateDataset> datasets)
    {
        if (datasets == null)
        {
            throw new DomainValidationException("Datasets collection cannot be null.");
        }

        var datasetList = datasets.ToList();
        foreach (var d in datasetList)
        {
            if (d == null)
            {
                throw new DomainValidationException("TemplateDataset item cannot be null.");
            }

            if (d.TemplateId != Id)
            {
                throw new DomainValidationException($"TemplateDataset template ID '{d.TemplateId}' does not match template ID '{Id}'.");
            }
        }

        var aliases = datasetList.Select(d => d.Alias.Trim().ToLowerInvariant()).ToList();
        if (aliases.Count != aliases.Distinct().Count())
        {
            throw new BusinessRuleViolationException(
                "Dataset aliases must be unique within a template.",
                "DUPLICATE_DATASET_ALIAS");
        }

        _templateDatasets.Clear();
        _templateDatasets.AddRange(datasetList);
        SetUpdated();
    }

    public void UpdateDetails(string name, string? category)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Template name cannot be empty or whitespace.");
        }

        Name = name.Trim();
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        SetUpdated();
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        SetUpdated();
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        SetUpdated();
    }
}
