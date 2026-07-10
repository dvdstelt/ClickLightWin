namespace ClickLightWin.Tests;

public class SettingsStoreTests
{
    [Theory]
    [InlineData(32, 32)]   // exact preset stays
    [InlineData(28, 32)]   // pre-preset default snaps to Medium
    [InlineData(22, 22)]   // Small
    [InlineData(5, 22)]    // below range clamps to the smallest
    [InlineData(500, 60)]  // above range clamps to the largest
    public void Sizes_snap_to_the_nearest_preset(double stored, double expected) =>
        Assert.Equal(expected, SettingsStore.Nearest(Presets.Sizes, stored));

    [Theory]
    [InlineData(480, 480)] // exact preset stays
    [InlineData(450, 480)] // pre-preset default snaps to Normal
    [InlineData(1000, 1000)]
    public void Durations_snap_to_the_nearest_preset(double stored, double expected) =>
        Assert.Equal(expected, SettingsStore.Nearest(Presets.Durations, stored));

    [Fact]
    public void Normalize_snaps_both_numeric_settings()
    {
        var settings = new Settings { BaseDiameterDips = 28, PulseDurationMs = 450 };
        SettingsStore.Normalize(settings);
        Assert.Equal(32, settings.BaseDiameterDips);
        Assert.Equal(480, settings.PulseDurationMs);
    }
}
