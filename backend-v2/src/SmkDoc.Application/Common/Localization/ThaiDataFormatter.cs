using SmkDoc.Application.Common.Helpers;

namespace SmkDoc.Application.Common.Localization;

/// <summary>
/// Thai (<c>th-TH</c>) implementation of <see cref="ILocalizedDataFormatter"/> delegating to stateless
/// formatting helpers (BahtText, Buddhist Era dates, Thai numerals, phone/ID formatting).
/// </summary>
public sealed class ThaiDataFormatter : ILocalizedDataFormatter
{
    public string CultureCode => "th-TH";

    public string Transform(string input, string transformType) =>
        ThaiDataTransformer.Transform(input, transformType);
}
