using System;

namespace Adamantium.UI.Core.Resources;

public interface IThemeManager : IThemeContext
{
    ITheme CurrentTheme { get; }

    /// <summary>Raised SYNCHRONOUSLY at the start of <see cref="SetTheme"/>, before anything is re-styled - the moment to
    /// put a busy overlay up. <see cref="IsThemeChanging"/> is already true here.</summary>
    event EventHandler<ThemeChangedEventArgs> ThemeChanging;

    /// <summary>Raised when the swap has fully SETTLED - not when <see cref="SetTheme"/> returns. Applying a theme
    /// re-styles, re-templates and re-lays-out the tree over several layout passes, so the work is far from done when the
    /// call returns; this fires on the first pass that finds nothing left to do, in every window the swap touched.</summary>
    event EventHandler<ThemeChangedEventArgs> ThemeChanged;

    /// <summary>True between <see cref="ThemeChanging"/> and <see cref="ThemeChanged"/> - i.e. while the swap's cascade is
    /// still draining. What a busy indicator is driven by.</summary>
    bool IsThemeChanging { get; }

    void AddTheme(string name, ITheme theme);

    void RemoveTheme(string name);

    void SetTheme(ITheme theme);

    void ApplyTheme(ITheme theme, IFundamentalUIComponent component);
    
    void ApplyTheme(string name, IFundamentalUIComponent component);

    void RemoveStyles(IFundamentalUIComponent component);

    Style[] FindStylesForComponent(IFundamentalUIComponent component);

    ITheme this[string name] { get; }
    
    ITheme this[int index] { get; }
}