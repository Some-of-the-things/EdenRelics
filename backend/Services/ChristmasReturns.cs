namespace Eden_Relics_BE.Services;

/// <summary>
/// The extended Christmas returns window for website orders. The same dates are printed on
/// Teodora's gift card, so don't change them without telling her. Mirrored on the frontend in
/// <c>christmas-returns.ts</c>.
/// </summary>
public static class ChristmasReturns
{
    public static readonly DateOnly OrderedFrom = new(2026, 11, 1);
    public static readonly DateOnly OrderedTo = new(2026, 12, 24);
    public static readonly DateOnly ReturnBy = new(2027, 1, 23);

    public const string PolicyText =
        "Orders placed between 1 November and 24 December can be returned until 23 January 2027, "
        + "as long as they're unworn and in the condition we sent them.";

    private static readonly TimeZoneInfo UkTime = ResolveUkTime();

    /// <summary>Whether an order placed at <paramref name="placedAtUtc"/> (UK calendar date) falls in the window.</summary>
    public static bool Covers(DateTime placedAtUtc)
    {
        DateTime utc = DateTime.SpecifyKind(placedAtUtc, DateTimeKind.Utc);
        DateOnly ukDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, UkTime));
        return ukDate >= OrderedFrom && ukDate <= OrderedTo;
    }

    private static TimeZoneInfo ResolveUkTime()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        }
        catch (TimeZoneNotFoundException)
        {
            // Nov–Dec is GMT, so UTC gives the same calendar date for the whole window.
            return TimeZoneInfo.Utc;
        }
    }
}
