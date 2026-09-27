using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SmkDoc.Domain.Common;

public abstract class Enumeration : IComparable
{
    public string Name { get; private set; }
    public int Id { get; private set; }

    protected Enumeration(int id, string name) => (Id, Name) = (id, name);

    public override string ToString() => Name;

    private static class Cache<T> where T : Enumeration
    {
        internal static readonly IReadOnlyList<T> All;
        internal static readonly Dictionary<int, T> ById;
        internal static readonly Dictionary<string, T> ByName;

        static Cache()
        {
            All = typeof(T).GetFields(BindingFlags.Public |
                                      BindingFlags.Static |
                                      BindingFlags.DeclaredOnly)
                           .Select(f => f.GetValue(null))
                           .Cast<T>()
                           .ToArray();

            ById = All.ToDictionary(x => x.Id);
            ByName = All.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        }
    }

    public static IEnumerable<T> GetAll<T>() where T : Enumeration => Cache<T>.All;

    public override bool Equals(object? obj)
    {
        if (obj is not Enumeration otherValue)
        {
            return false;
        }

        var typeMatches = GetType().Equals(obj.GetType());
        var valueMatches = Id.Equals(otherValue.Id);

        return typeMatches && valueMatches;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public int CompareTo(object? other) => Id.CompareTo(((Enumeration)other!).Id);

    public static T FromValue<T>(int value) where T : Enumeration
    {
        if (Cache<T>.ById.TryGetValue(value, out var matchingItem))
        {
            return matchingItem;
        }

        throw new InvalidOperationException($"'{value}' is not a valid value in {typeof(T)}");
    }

    public static T FromDisplayName<T>(string displayName) where T : Enumeration
    {
        if (displayName != null && Cache<T>.ByName.TryGetValue(displayName, out var matchingItem))
        {
            return matchingItem;
        }

        throw new InvalidOperationException($"'{displayName}' is not a valid display name in {typeof(T)}");
    }

    public static bool TryFromValue<T>(int value, [NotNullWhen(true)] out T? result) where T : Enumeration
    {
        return Cache<T>.ById.TryGetValue(value, out result);
    }

    public static bool TryFromDisplayName<T>(string? displayName, [NotNullWhen(true)] out T? result) where T : Enumeration
    {
        if (displayName != null)
        {
            return Cache<T>.ByName.TryGetValue(displayName, out result);
        }

        result = null;
        return false;
    }
}

