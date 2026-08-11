using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>The coloured PLATE a run of contextual tabs stands on, with the context's title along its top. Built and
/// placed by <see cref="Panels.RibbonTabPanel"/>: which tabs a plate spans is a fact about the STRIP's layout, and
/// nothing above the strip can know it.
/// <para>One plate for the whole run, drawn UNDER its tabs, rather than a colour on each tab: analytic anti-aliasing
/// gives every filled edge half coverage exactly on the edge, so two abutting fills compose to about three quarters
/// and leave a dark hairline down the join. One fill has no joins to leave.</para></summary>
public class RibbonContextualLedge : ContentControl
{
    /// <summary>Height of the title band at the top of the plate; the rest of it is what the tabs stand on. Zero when
    /// the context draws no title (<see cref="RibbonContextualGroup.ShowHeader"/>).</summary>
    public static readonly AdamantiumProperty TitleHeightProperty = AdamantiumProperty.Register(nameof(TitleHeight),
        typeof(double), typeof(RibbonContextualLedge),
        new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    public double TitleHeight
    {
        get => GetValue<double>(TitleHeightProperty);
        set => SetValue(TitleHeightProperty, value);
    }

    /// <summary>The group's colour. Its own property rather than Background, so the theme decides how the colour is
    /// used - a solid ledge, a rule, a wash.</summary>
    public static readonly AdamantiumProperty AccentProperty = AdamantiumProperty.Register(nameof(Accent),
        typeof(Brush), typeof(RibbonContextualLedge), new PropertyMetadata(default(Brush), PropertyMetadataOptions.AffectsRender));

    public Brush Accent
    {
        get => GetValue<Brush>(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    static RibbonContextualLedge()
    {
        // A label, not a target: it says what a run of tabs is, and clicking it would mean nothing. Hit-testing must
        // also stay off because the plate lies UNDER the tabs and spans them all.
        FocusableProperty.OverrideMetadata(typeof(RibbonContextualLedge), new PropertyMetadata(false));
        IsHitTestVisibleProperty.OverrideMetadata(typeof(RibbonContextualLedge), new PropertyMetadata(false));
    }

    public RibbonContextualLedge()
    {
        // UNDER the tabs it carries: visual children are drawn in the order they were added, and a plate is created
        // while the item containers are already there, so order alone would put it over their labels.
        // A real WRITE, not a metadata default: the render walk reads ZIndex from a plain field, and that field is
        // filled by the property's changed callback - which a default never fires.
        ZIndex = -1;
    }
}
