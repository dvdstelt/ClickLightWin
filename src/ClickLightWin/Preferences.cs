namespace ClickLightWin;

/// <summary>
/// User-toggleable state persisted across runs (distinct from the visual
/// <see cref="Settings"/>, which is not user-editable yet). Grows as the settings
/// window lands in a later milestone. Mirrors the persisted slice of SettingsStore.swift.
/// </summary>
public sealed class Preferences
{
    public bool Enabled { get; set; } = true;
}
