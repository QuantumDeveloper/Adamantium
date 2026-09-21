using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>A row of the backstage rail. It is one of two things, and which one it is depends on whether it has a page:
/// with one it OPENS that page beside the rail and stays lit while it shows; without one it is a plain command that runs
/// and closes the backstage behind it.</summary>
public class RibbonApplicationMenuItem : Button, ISelectable
{
    /// <summary>Held as an <c>object</c> with a template beside it, the way a command's icon is: path text converts to a
    /// Geometry on its own, and anything else can be drawn instead.</summary>
    public static readonly AdamantiumProperty IconProperty = AdamantiumProperty.Register(nameof(Icon),
        typeof(object), typeof(RibbonApplicationMenuItem), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty IconTemplateProperty = AdamantiumProperty.Register(nameof(IconTemplate),
        typeof(DataTemplate), typeof(RibbonApplicationMenuItem), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>What this row shows beside the rail. Null makes the row a command rather than a page.</summary>
    public static readonly AdamantiumProperty PageContentProperty = AdamantiumProperty.Register(nameof(PageContent),
        typeof(object), typeof(RibbonApplicationMenuItem), new PropertyMetadata(null));

    public static readonly AdamantiumProperty IsSelectedProperty = AdamantiumProperty.Register(nameof(IsSelected),
        typeof(bool), typeof(RibbonApplicationMenuItem), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    /// <summary>Marks the row whose page is showing. Null = no change in that state.</summary>
    public static readonly AdamantiumProperty BackgroundSelectedProperty = AdamantiumProperty.Register(
        nameof(BackgroundSelected), typeof(Brush), typeof(RibbonApplicationMenuItem), new PropertyMetadata(default(Brush)));

    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public DataTemplate IconTemplate
    {
        get => GetValue<DataTemplate>(IconTemplateProperty);
        set => SetValue(IconTemplateProperty, value);
    }

    public object PageContent
    {
        get => GetValue(PageContentProperty);
        set => SetValue(PageContentProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public Brush BackgroundSelected
    {
        get => GetValue<Brush>(BackgroundSelectedProperty);
        set => SetValue(BackgroundSelectedProperty, value);
    }
}
