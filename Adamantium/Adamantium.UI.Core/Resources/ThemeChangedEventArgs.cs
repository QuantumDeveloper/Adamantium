using System;

namespace Adamantium.UI.Core.Resources;

/// <summary>Which theme is being replaced by which. Carried by both <see cref="IThemeManager.ThemeChanging"/> (before the
/// cascade starts) and <see cref="IThemeManager.ThemeChanged"/> (once it has settled).</summary>
public class ThemeChangedEventArgs(ITheme oldTheme, ITheme newTheme) : EventArgs
{
    /// <summary>The theme that was current before the swap. Null on the first theme ever applied.</summary>
    public ITheme OldTheme { get; } = oldTheme;

    public ITheme NewTheme { get; } = newTheme;
}
