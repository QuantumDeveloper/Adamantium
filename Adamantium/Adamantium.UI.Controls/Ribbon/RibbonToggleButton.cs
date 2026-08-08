using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>A ribbon command that holds a state - bold, wireframe, snap-to-grid. Same shape as
/// <see cref="RibbonButton"/>, with <see cref="ToggleButton.IsChecked"/> on top.</summary>
public class RibbonToggleButton : ToggleButton
{
    /// <summary>What marks the command, drawn by <see cref="IconTemplate"/> - see <see cref="RibbonButton.IconProperty"/>
    /// for why it is data rather than a control.</summary>
    public static readonly AdamantiumProperty IconProperty = AdamantiumProperty.Register(nameof(Icon),
        typeof(object), typeof(RibbonToggleButton), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty IconTemplateProperty = AdamantiumProperty.Register(nameof(IconTemplate),
        typeof(DataTemplate), typeof(RibbonToggleButton), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>How <see cref="Icon"/> is drawn; the theme supplies a default that renders path data.</summary>
    public DataTemplate IconTemplate
    {
        get => GetValue<DataTemplate>(IconTemplateProperty);
        set => SetValue(IconTemplateProperty, value);
    }

    // The ribbon's sizing under this type - the SAME property objects. See RibbonButton for why this needs no AddOwner.
    public static readonly AdamantiumProperty SizeProperty = Ribbon.SizeProperty;

    public static readonly AdamantiumProperty CollapseToMediumProperty = Ribbon.CollapseToMediumProperty;

    public static readonly AdamantiumProperty CollapseToSmallProperty = Ribbon.CollapseToSmallProperty;

    public static readonly AdamantiumProperty MaxSizeProperty = Ribbon.MaxSizeProperty;

    /// <summary>The size this command is CURRENTLY drawn at - the group's answer, never the author's.</summary>
    public RibbonSize Size
    {
        get => GetValue<RibbonSize>(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>At which step of its group this command drops its big icon for a small one beside the label.</summary>
    public RibbonCollapseThreshold CollapseToMedium
    {
        get => GetValue<RibbonCollapseThreshold>(CollapseToMediumProperty);
        set => SetValue(CollapseToMediumProperty, value);
    }

    /// <summary>...and at which step it drops the label too. A command nobody recognises without its words says Never.</summary>
    public RibbonCollapseThreshold CollapseToSmall
    {
        get => GetValue<RibbonCollapseThreshold>(CollapseToSmallProperty);
        set => SetValue(CollapseToSmallProperty, value);
    }

    /// <summary>The largest this command may be drawn at.</summary>
    public RibbonSize MaxSize
    {
        get => GetValue<RibbonSize>(MaxSizeProperty);
        set => SetValue(MaxSizeProperty, value);
    }
}
