namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Factory interface for resolving polymorphic SQL driver strategies based on database provider.
/// </summary>
public interface ISqlDriverFactory
{
    /// <summary>
    /// Resolves the driver strategy for the specified provider (case-insensitive).
    /// </summary>
    ISqlDriverStrategy GetStrategy(string provider);
}

