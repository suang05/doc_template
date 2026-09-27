namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Cache abstraction for compiled template delegates to avoid repetitive AST parsing and compilation overhead.
/// </summary>
public interface ICompiledTemplateCache
{
    /// <summary>
    /// Gets a cached compiled template evaluation function or computes and adds it using the factory.
    /// </summary>
    Func<object, string> GetOrAdd(string key, Func<Func<object, string>> factory);
}
