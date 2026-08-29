using Adamantium.Graphics.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Core.Resources;

public interface ITheme: IInitializable, IAdamantiumComponent
{
    string Name { get; }

    Brush AccentColor { get; set; }

    // The theme's accent / focus brushes: its runtime-mutable identity, consumed across styles via {ThemeResource Key}.
    // Assigning one live re-colours every consumer (checked toggles/checkboxes/radios, focus) with no theme reload.
    Brush AccentFillColorDefault { get; set; }

    Brush AccentFillColorSecondary { get; set; }

    Brush AccentFillColorTertiary { get; set; }

    /// <summary>The accent as a WASH - what marks a selected row. Translucent on purpose: an interface built in layers
    /// (and, before long, over a blurred backdrop) has nothing else that is a flat slab, and a solid accent under a
    /// row's own translucent tile simply replaces it - the tile has no weight of its own to survive with.</summary>
    Brush AccentFillColorSelection { get; set; }

    /// <summary>The same wash, denser - a selected row under the pointer. A wash strengthens by becoming more opaque;
    /// darkening it, the way the solid ramp does, barely reads at this alpha.</summary>
    Brush AccentFillColorSelectionStrong { get; set; }

    Brush AccentFillColorDisabled { get; set; }

    Brush AccentForegroundColor { get; set; }

    Brush FocusStrokeColorOuter { get; set; }

    Brush FocusStrokeColorInner { get; set; }

    /// <summary>The theme's font for text - consumed in styles via {ThemeResource FontFamily} and inherited via
    /// UIComponent.FontFamily.</summary>
    FontFamily FontFamily { get; }
    
    /// <summary>The variants this theme declares, by key. A theme with none has exactly one appearance.</summary>
    IReadOnlyDictionary<ThemeVariant, ThemeVariantDefinition> VariantsByKey { get; }

    /// <summary>The variant in force. Setting it re-colours the palette IN PLACE - the brushes keep their identity, so
    /// nothing that draws with them has to be told anything beyond "you changed".</summary>
    ThemeVariant CurrentVariant { get; }

    /// <summary>Make <paramref name="variant"/> current. Returns false if this theme does not declare it - the caller
    /// then knows to fall back rather than being silently given something else.</summary>
    bool ApplyVariant(ThemeVariant variant);

    /// <summary>Which of this theme's variants answers to the operating system saying "light" / "dark". Unspecified
    /// when the theme has no such notion - a HUD theme is dark by nature and its variants run along another axis
    /// entirely - and then following the system means staying on the default variant.</summary>
    ThemeVariant SystemLightVariant { get; }

    ThemeVariant SystemDarkVariant { get; }

    /// <summary>Resolve <see cref="ThemeVariant.System"/> against what the OS currently says. Returns unspecified when
    /// this theme has no light/dark mapping.</summary>
    ThemeVariant ResolveSystemVariant(bool osPrefersDark);

    StyleSetCollection StyleSets { get; }
    
    StyleIncludeCollection StyleIncludes { get; }

    void AddStyleSet(StyleSet styleSet);
    
    StyleSet MergedStyles { get; }

    Style[] FindStylesForComponent(IFundamentalUIComponent component);
    
    object GetResource(string key);

    bool TryGetResource(string key, out object value);

    // Requester-aware variants: resolve Local resources tree-scoped from the requesting element (then Theme, then
    // Global). Setters and triggers use these so a {ResourceReference} sees only the Local dictionaries on its own
    // subtree's ancestors.
    object GetResource(IFundamentalUIComponent requester, string key);

    bool TryGetResource(IFundamentalUIComponent requester, string key, out object value);
}