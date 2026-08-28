using System;

namespace Adamantium.UI.Core.Resources;

public interface IThemeManager : IThemeEngine
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

    /// <summary>A MINIMUM time the busy overlay stays up once a swap begins (seconds). 0 (default) = finish the moment the
    /// cascade drains; set higher to keep the swap loader on screen long enough to see it spin.</summary>
    double MinSwapSeconds { get; set; }

    void AddTheme(string name, ITheme theme);

    void RemoveTheme(string name);

    void SetTheme(ITheme theme);

    /// <summary>Switch the current theme's VARIANT - light to dark, or whatever else it declares. Returns false if the
    /// theme does not have that variant, so the caller knows rather than being handed something else.
    /// <para>This is deliberately NOT <see cref="SetTheme"/> with a second theme, and the difference is the whole point
    /// of variants: the styles and templates are the same objects before and after, so nothing is re-templated, nothing
    /// is re-styled, and no element is written to. What changes is the COLOUR inside palette brushes that are already
    /// hanging on the elements - O(palette keys), around a hundred, instead of O(elements), around twenty thousand.</para>
    /// <para>It also raises no swap events and does not move the theme version: a parked subtree that comes back after a
    /// variant change is already correct, because it is holding the very brushes whose colour changed.</para></summary>
    bool SetVariant(ThemeVariant variant);

    void ApplyTheme(ITheme theme, IFundamentalUIComponent component);
    
    void ApplyTheme(string name, IFundamentalUIComponent component);

    void RemoveStyles(IFundamentalUIComponent component);

    Style[] FindStylesForComponent(IFundamentalUIComponent component);

    ITheme this[string name] { get; }
    
    ITheme this[int index] { get; }
}