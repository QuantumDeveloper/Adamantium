using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>A command in a <see cref="RibbonGroup"/>: an <see cref="Icon"/> over (or beside) its label, running a
/// <see cref="Primitives.ButtonBase.Command"/>. The label is the Content; how big it draws is
/// <see cref="Ribbon.SizeProperty"/>, which the group sets within the author's Min/MaxSize range.</summary>
public class RibbonButton : Button
{
    /// <summary>What marks the command - DATA drawn by <see cref="IconTemplate"/>, the same shape
    /// <see cref="TabItem.Icon"/> has: one command may be drawn in two places at once (its group and the quick-access
    /// bar), and a control can only be in one. The SAME property object the ribbon attaches, so anything that has to
    /// read a command's icon without knowing its type can.</summary>
    public static readonly AdamantiumProperty IconProperty = Ribbon.IconProperty;

    /// <summary>How <see cref="Icon"/> is drawn. The theme default renders path data.</summary>
    public static readonly AdamantiumProperty IconTemplateProperty = Ribbon.IconTemplateProperty;

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

    // The ribbon's sizing under this type - the SAME property objects, so `<RibbonButton MaxSize="Medium"/>` and
    // `Ribbon.SetMaxSize(button, ...)` write one slot. The CLR property is what an unprefixed attribute needs; the
    // fields also make touching this type register the attached ones (its initializer touches Ribbon's).
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
