using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>A set of tabs that appear under a task - select a mesh and "Mesh Tools" shows up. A DESCRIPTION, not a
/// control: one group is worn by several tabs at once, and a control can only be in one place. The tabs point at it
/// (<see cref="RibbonTab.ContextualGroup"/>); it says nothing about them.</summary>
public class RibbonContextualGroup : AdamantiumComponent
{
    /// <summary>What a tab names this group by (<see cref="RibbonTab.ContextualGroupKey"/>). A key rather than an
    /// element name because a group is not a visual, and rather than the header because two contexts may legitimately
    /// be called the same thing.</summary>
    public static readonly AdamantiumProperty KeyProperty = AdamantiumProperty.Register(nameof(Key),
        typeof(string), typeof(RibbonContextualGroup), new PropertyMetadata(null));

    public string Key
    {
        get => GetValue<string>(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    /// <summary>What the ledge over the group's tabs says.</summary>
    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(object), typeof(RibbonContextualGroup), new PropertyMetadata(null));

    /// <summary>The colour that marks everything belonging to this context: the ledge, its tabs' headers, and the top
    /// edge of the groups area - so an open page still says which context it belongs to once the ledge is out of view.</summary>
    public static readonly AdamantiumProperty AccentProperty = AdamantiumProperty.Register(nameof(Accent),
        typeof(Brush), typeof(RibbonContextualGroup), new PropertyMetadata(default(Brush)));

    /// <summary>Whether the ledge over the group's tabs is drawn at all. Off, the context is carried by the COLOUR of
    /// its tabs alone - which is what actually says which tabs belong together - and the strip costs no extra height.
    /// Microsoft dropped the ledge in current M365 for the same reason.</summary>
    public static readonly AdamantiumProperty ShowHeaderProperty = AdamantiumProperty.Register(nameof(ShowHeader),
        typeof(bool), typeof(RibbonContextualGroup), new PropertyMetadata(true));

    public bool ShowHeader
    {
        get => GetValue<bool>(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    /// <summary>Whether the group's tabs are in the strip at all. Bound to whatever the application means by "this
    /// context applies" - a selection, a mode, a running tool.</summary>
    public static readonly AdamantiumProperty IsActiveProperty = AdamantiumProperty.Register(nameof(IsActive),
        typeof(bool), typeof(RibbonContextualGroup), new PropertyMetadata(false));

    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public Brush Accent
    {
        get => GetValue<Brush>(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public bool IsActive
    {
        get => GetValue<bool>(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>When this group last became active. Groups are laid out in this order, so tabs that appeared LAST stand
    /// furthest right and do not shift what someone was already aiming at. Kept by the ribbon.</summary>
    internal long ActivatedAt { get; set; }
}
