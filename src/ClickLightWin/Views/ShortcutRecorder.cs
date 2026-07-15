using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ClickLightWin.Views;

/// <summary>
/// A button that records a global hotkey: click it, then press a key combination.
/// It shows the current binding (e.g. "Ctrl+Shift+L") and updates <see cref="Binding"/>
/// two-way. Esc or losing focus cancels. Requires at least one of Ctrl/Alt/Win so
/// the captured combo works as a global hotkey. Maps to ShortcutRecorderField.swift.
/// </summary>
public sealed class ShortcutRecorder : Button
{
    public static readonly DependencyProperty BindingProperty = DependencyProperty.Register(
        nameof(Binding), typeof(HotKeyBinding), typeof(ShortcutRecorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBindingChanged));

    private bool _recording;

    public ShortcutRecorder()
    {
        Focusable = true;
        Click += (_, _) => StartRecording();
        LostKeyboardFocus += (_, _) => StopRecording();
        UpdateText();
    }

    public HotKeyBinding? Binding
    {
        get => (HotKeyBinding?)GetValue(BindingProperty);
        set => SetValue(BindingProperty, value);
    }

    private void StartRecording()
    {
        _recording = true;
        Keyboard.Focus(this);
        UpdateText();
    }

    private void StopRecording()
    {
        if (!_recording) return;
        _recording = false;
        UpdateText();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!_recording)
        {
            base.OnPreviewKeyDown(e);
            return;
        }
        e.Handled = true; // never let the recorded keys do anything else

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { StopRecording(); return; }
        if (IsModifierKey(key)) return; // wait for the non-modifier key

        // Require Ctrl/Alt/Win so the combo is a usable global hotkey (Shift alone is typing).
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0)
            return;

        Binding = new HotKeyBinding(Keyboard.Modifiers, key);
        StopRecording();
    }

    private static bool IsModifierKey(Key k) => k is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift
        or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System;

    private static void OnBindingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ShortcutRecorder)d).UpdateText();

    private void UpdateText() => Content = _recording ? "Press a shortcut…" : Binding?.Display ?? "None";
}
