using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>A named cluster of commands inside a <see cref="RibbonTab"/>, its caption under them. Items are the
/// commands, laid out in columns by <see cref="Panels.RibbonGroupPanel"/>.</summary>
public class RibbonGroup : ItemsControl, IHeaderedItemsControl
{
    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(object), typeof(RibbonGroup), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty HeaderTemplateProperty = AdamantiumProperty.Register(nameof(HeaderTemplate),
        typeof(DataTemplate), typeof(RibbonGroup), new PropertyMetadata(null));

    /// <summary>Whether the rule dividing this group from the next is drawn. Maintained by the owning
    /// <see cref="RibbonTab"/>, which turns it off on the LAST group.</summary>
    public static readonly AdamantiumProperty ShowSeparatorProperty = AdamantiumProperty.Register(nameof(ShowSeparator),
        typeof(bool), typeof(RibbonGroup), new PropertyMetadata(true, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>The group's caption, drawn under its commands.</summary>
    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool ShowSeparator
    {
        get => GetValue<bool>(ShowSeparatorProperty);
        set => SetValue(ShowSeparatorProperty, value);
    }

    public DataTemplate HeaderTemplate
    {
        get => GetValue<DataTemplate>(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }
}
