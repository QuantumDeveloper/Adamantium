using System;

namespace Adamantium.UI.Core.Resources;

/// <summary>
/// What the operating system is currently asking interfaces to look like. The platform layer sets it - from the
/// Windows personalisation setting, from the macOS effective appearance - and everything that resolves
/// <see cref="ThemeVariant.System"/> reads it here.
/// </summary>
/// <remarks>
/// Platform-neutral on purpose, and one value rather than a value per window: the OS says light or dark for the whole
/// session, and a per-window copy would be a second place for the same fact to live in - the shape that has already
/// cost this codebase a quadratic and a leak.
/// <para>It is a SIGNAL, not a poll. The OS announces a change (a message on Windows, a notification on macOS), so
/// nothing here asks repeatedly; <see cref="Changed"/> fires when the answer actually differs, and only then.</para>
/// </remarks>
public static class SystemAppearance
{
    private static bool _prefersDark;

    /// <summary>Whether the OS currently asks for a dark appearance. Written by the platform layer.</summary>
    public static bool PrefersDark
    {
        get => _prefersDark;
        set
        {
            if (_prefersDark == value) return;   // only a real change is worth telling anyone about
            _prefersDark = value;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Raised when the OS appearance actually changes - day turning to night, or the user flipping the
    /// setting. Handlers must not throw.</summary>
    public static event EventHandler Changed;
}
