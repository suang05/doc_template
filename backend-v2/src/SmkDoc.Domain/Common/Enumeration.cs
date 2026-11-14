using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Common;

[SuppressMessage("Naming", "CA1724:Type names should not match namespaces", Justification = "Domain SmartEnum base class name intentionally preserved across domain/application contracts.")]
public abstract class Enumeration(int id, string name) : IComparable
{
    public string Name { get; } = name;
    public int Id { get; } = id;

    public override string ToString() => Name;

    private static class Cache<T> where T : Enumeration
    {
        internal static readonly IReadOnlyList<T> All = BuildAll();
        internal static readonly Dictionary<int, T> ById = All.ToDictionary(x => x.Id);
        internal static readonly Dictionary<string, T> ByName = All.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        private static T[] BuildAll() =>
            typeof(T).GetFields(BindingFlags.Public |
                                BindingFlags.Static |
                                BindingFlags.DeclaredOnly)
                .Select(f => f.GetValue(null))
                .Cast<T>()
                .ToArray();
    }

    public static IEnumerable<T> GetAll<T>() where T : Enumeration => Cache<T>.All;

    public override bool Equals(object? obj)
    {
        if (obj is not Enumeration otherValue)
        {
            return false;
        }

        var typeMatches = GetType() == obj.GetType();
        var valueMatches = Id.Equals(otherValue.Id);

        return typeMatches && valueMatches;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is not Enumeration other)
        {
            throw new ArgumentException($"Object must be of type {nameof(Enumeration)}.", nameof(obj));
        }

        return Id.CompareTo(other.Id);
    }

    public static bool operator ==(Enumeration? left, Enumeration? right) =>
        Equals(left, right);

    public static bool operator !=(Enumeration? left, Enumeration? right) =>
        !Equals(left, right);

    public static bool operator <(Enumeration? left, Enumeration? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(Enumeration? left, Enumeration? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >(Enumeration? left, Enumeration? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator >=(Enumeration? left, Enumeration? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;

    public static T FromValue<T>(int value) where T : Enumeration
    {
        if (Cache<T>.ById.TryGetValue(value, out var matchingItem))
        {
            return matchingItem;
        }

        throw new DomainValidationException($"'{value}' is not a valid value in {typeof(T).Name}");
    }

    public static T FromDisplayName<T>(string displayName) where T : Enumeration
    {
        if (!string.IsNullOrWhiteSpace(displayName) && Cache<T>.ByName.TryGetValue(displayName, out var matchingItem))
        {
            return matchingItem;
        }

        throw new DomainValidationException($"'{displayName}' is not a valid display name in {typeof(T).Name}");
    }

    public static bool TryFromValue<T>(int value, [NotNullWhen(true)] out T? result) where T : Enumeration
    {
        return Cache<T>.ById.TryGetValue(value, out result);
    }

    public static bool TryFromDisplayName<T>(string? displayName, [NotNullWhen(true)] out T? result)
        where T : Enumeration
    {
        if (displayName != null)
        {
            return Cache<T>.ByName.TryGetValue(displayName, out result);
        }

        result = null;
        return false;
    }
}
