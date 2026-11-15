namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Open/Closed locale-specific data formatting strategy used by the DataBinding engine.
/// </summary>
public interface ILocalizedDataFormatter
{
    string CultureCode { get; }
    string Transform(string input, string transformType);
}
