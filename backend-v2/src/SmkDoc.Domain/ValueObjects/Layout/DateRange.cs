using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.ValueObjects.Layout;

/// <summary>
/// Lightweight, zero-allocation 8-byte immutable date range.
/// Enforces business invariant that Start date must precede or equal End date.
/// </summary>
public readonly record struct DateRange
{
    public DateOnly Start { get; }
    public DateOnly End { get; }

    public DateRange(DateOnly start, DateOnly end)
    {
        if (start > end)
        {
            throw new DomainValidationException(
                $"Date range start '{start:yyyy-MM-dd}' cannot be after end '{end:yyyy-MM-dd}'.",
                "DATE_RANGE_INVALID");
        }

        Start = start;
        End = end;
    }

    public int TotalDays => End.DayNumber - Start.DayNumber + 1;

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public override string ToString() => $"{Start:yyyy-MM-dd}..{End:yyyy-MM-dd} ({TotalDays} days)";
}
