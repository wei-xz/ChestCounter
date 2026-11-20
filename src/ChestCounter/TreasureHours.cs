namespace ChestCounter;

/// <summary>
/// Saturday "Treasure Hours": the Pirate Shop Access odds are multiplied by 1.5 (1 in 137,500 becomes 1 in 91,667).
/// Times are US Eastern. Edit <see cref="Windows"/> if Epic changes the schedule.
/// </summary>
internal static class TreasureHours
{
    public const double Boost = 1.5;

    /// <summary>Date (Eastern), start hour, end hour. 14 Nov ran 2 to 6 PM instead of the usual two sessions.</summary>
    private static readonly (DateTime Date, double From, double To)[] Windows =
    {
        (new DateTime(2026, 11, 7), 14, 16), (new DateTime(2026, 11, 7), 21, 23),
        (new DateTime(2026, 11, 14), 14, 18),
        (new DateTime(2026, 11, 21), 14, 16), (new DateTime(2026, 11, 21), 21, 23),
        (new DateTime(2026, 11, 28), 14, 16), (new DateTime(2026, 11, 28), 21, 23),
    };

    private static readonly TimeZoneInfo Eastern = FindEastern();

    private static TimeZoneInfo FindEastern()
    {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception)
            {
                // try the next name (Windows and Linux do not use the same ids)
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("ET", TimeSpan.FromHours(-5), "ET", "ET");
    }

    /// <summary>True when the given UTC moment falls inside a Treasure Hours window.</summary>
    public static bool Contains(DateTime utc)
    {
        var et = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Eastern);
        foreach (var w in Windows)
        {
            if (et.Date == w.Date && et.TimeOfDay.TotalHours >= w.From && et.TimeOfDay.TotalHours < w.To)
            {
                return true;
            }
        }

        return false;
    }
}
