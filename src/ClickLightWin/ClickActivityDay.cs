using System.Globalization;
using System.Text.Json.Serialization;

namespace ClickLightWin;

/// <summary>
/// One day's click tallies, kept locally for the Activity view. Left/Right/Middle
/// count button presses; Drags counts drag gestures. Mirrors the macOS
/// ClickActivityDay. The date is a stable "yyyy-MM-dd" key so it round-trips through
/// JSON without culture surprises.
/// </summary>
public sealed class ClickActivityDay
{
    public string Date { get; set; } = "";
    public int Left { get; set; }
    public int Right { get; set; }
    public int Middle { get; set; }
    public int Drags { get; set; }

    [JsonIgnore] public int TotalClicks => Left + Right + Middle;

    /// <summary>Short weekday label (e.g. "Mon") for the chart axis.</summary>
    [JsonIgnore]
    public string Label =>
        DateTime.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d.ToString("ddd", CultureInfo.CurrentCulture) : "";
}
