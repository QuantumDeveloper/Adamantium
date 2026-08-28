using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Resources;

/// <summary>
/// One variant of a theme: the COLOURS its palette resolves to, plus the theme values that go with them. Not the
/// brushes - those belong to the theme and are shared by every variant, which is the whole reason switching a variant
/// is cheap (see <see cref="Theme.ApplyVariant"/>).
/// </summary>
/// <remarks>
/// Two channels, because the theme answers by two:
/// <list type="bullet">
/// <item><see cref="Colors"/> feeds the palette - what <c>{ResourceReference SolidBackgroundFillColorBase}</c> finds.</item>
/// <item><see cref="Values"/> feeds the theme's own PROPERTIES - what <c>{ThemeResource AccentColor}</c> finds, which
/// resolves against the theme object rather than any dictionary. Accent and focus live there, so a variant that could
/// only set colours would leave a light theme wearing the dark theme's accent.</item>
/// </list>
/// </remarks>
/// <para>Open for inheritance so a variant can live in its OWN markup file: a file whose root is this type generates a
/// class deriving from it, and the theme then names that class instead of restating four hundred lines of palette.</para>
public class ThemeVariantDefinition : IThemeVariant
{
    public ThemeVariantDefinition() { }

    public ThemeVariantDefinition(ThemeVariant key) => Key = key;

    /// <summary>Which variant this is - <c>Light</c>, <c>Dark</c>, or whatever this theme chooses to call it.</summary>
    public ThemeVariant Key { get; set; }

    /// <summary>Palette colours by resource key - child elements in markup, an indexer in code. Every variant of a
    /// theme must declare the SAME set of keys: a key one variant answers and another does not would make the
    /// subtree's appearance depend on which variant it happened to be switched FROM, which is not a thing anyone can
    /// reason about. See <see cref="Theme.ValidateVariants"/>, which is where that is caught.</summary>
    public PaletteColorCollection Colors { get; } = new();

    /// <summary>Theme PROPERTY values - <c>AccentColor</c>, <c>FocusStrokeColorOuter</c>. Applied to the theme when
    /// this variant becomes current. See <see cref="ThemeValue"/> for why these are not palette entries.</summary>
    public ThemeValueCollection Values { get; } = new();

    public override string ToString() => $"{Key} ({Colors.Count} colours, {Values.Count} values)";
}
