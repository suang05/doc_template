using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Data.Drivers;

/// <summary>
/// Polymorphic factory resolving SQL driver strategies based on database provider.
/// </summary>
public sealed class SqlDriverFactory(IEnumerable<ISqlDriverStrategy> strategies) : ISqlDriverFactory
{
    private readonly Dictionary<string, ISqlDriverStrategy> _strategies =
        strategies.ToDictionary(s => s.ProviderName, s => s, StringComparer.OrdinalIgnoreCase);

    public ISqlDriverStrategy GetStrategy(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new NotSupportedException("Database provider must be specified.");
        }

        if (_strategies.TryGetValue(provider.Trim(), out var strategy))
        {
            return strategy;
        }

        throw new NotSupportedException($"Data connection provider '{provider}' is not currently supported.");
    }
}

