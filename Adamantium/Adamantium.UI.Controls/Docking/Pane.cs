using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// The UNIT of docking: one panel with a header and content, plus the policy saying where it may live. There can be
/// hundreds of them inside one <see cref="DockingArea"/>.
/// <para>The policy belongs HERE and nowhere else - not in a table keyed by view-model type, which would mean writing
/// an entry per panel type and inventing a second mechanism. These are ordinary control properties, so markup sets them
/// as attributes and data-driven panes get them from an <c>ItemContainerStyle</c>, exactly like any other items control.</para>
/// <para>Built on <see cref="TabItem"/> deliberately: tabs, reordering and tearing off into a window already work
/// there, and a docking pane is a tab that also knows where it is allowed to be.</para>
/// </summary>
public class Pane : TabItem
{
    /// <summary>Where this pane sits. STATE, not a command - it is two-way bindable, so a drag writes it and a
    /// view-model can read it, or set it (<c>Placement = DockZone.Floating</c>) to send the pane away. One setter for
    /// both directions means the gesture and the code cannot drift apart.</summary>
    public static readonly AdamantiumProperty ZoneProperty = AdamantiumProperty.Register(nameof(Zone),
        typeof(DockZone), typeof(Pane), new PropertyMetadata(DockZone.Center));

    /// <summary>Where this pane MAY go. The whole vocabulary of restrictions is data, so it serialises and can be read
    /// at a glance; the rare "not here, but only on Tuesdays" case is served by the cancellable docking event instead
    /// of a predicate nobody can see.</summary>
    public static readonly AdamantiumProperty AllowedProperty = AdamantiumProperty.Register(nameof(Allowed),
        typeof(DockZone), typeof(Pane), new PropertyMetadata(DockZone.All));

    /// <summary>Smallest useful size along the axis this pane is DOCKED on - a width for a pane on the left or right, a
    /// height for one at the top or bottom, both for one in the centre, which has no single axis. This is what bounds
    /// nesting: a split is refused when a side would fall below it. A depth limit would be arbitrary - nobody cares
    /// which level a pane is on, they care that it became too narrow to use - and this rule also adapts itself to the
    /// size of the window.
    /// <para>Zero by default: NO opinion. The floor that always applies is the group's own tab strip, which is measured
    /// rather than invented - collapsing a pane down to its strip is what every editor does, and it is the point below
    /// which there is nothing left to grab. A default of "120 because that sounds small" only takes that away, and
    /// takes it away from panes whose author never asked for a limit at all.</para></summary>
    public static readonly AdamantiumProperty MinSizeProperty = AdamantiumProperty.Register(nameof(MinSize),
        typeof(double), typeof(Pane), new PropertyMetadata(0.0));

    /// <summary>Document or tool - see <see cref="PaneKind"/>. This is POLICY, not looks: what closing this pane means (a
    /// document goes, a tool is put away and comes back from a menu), and whether it is restored with a saved layout.
    /// <para>How its group is DRESSED comes from where the group stands, not from here - see
    /// <see cref="PaneGroup.KindProperty"/>. A tool dropped into the document area is dressed as a document, because the
    /// zones for tools are the edges and there is no edge in the centre to fold against; it stays a tool for everything
    /// this property is actually about.</para>
    /// <para>Documents by default, so a plain pane keeps the plain look; an editor marks its tools.</para></summary>
    public static readonly AdamantiumProperty KindProperty = AdamantiumProperty.Register(nameof(Kind),
        typeof(PaneKind), typeof(Pane), new PropertyMetadata(PaneKind.Document));

    public PaneKind Kind
    {
        get => GetValue<PaneKind>(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Which way this pane's TAB is turned. Set by the owning group when it folds down against a side edge: a
    /// collapsed panel there is a narrow column, and a horizontal label does not fit in one.
    /// <para>A property with three states rather than an angle, because the theme has to select a TEMPLATE from it - a
    /// Transform is not a component and so cannot be a binding target, which leaves a literal angle per state as the
    /// only way to say it.</para></summary>
    // AffectsParentMeasure as well as AffectsMeasure: turning the label swaps what the pane asks for - it stops being as
    // wide as its text and starts being as TALL as it - and how much room a child needs is the PARENT's business. On its
    // own, AffectsMeasure re-measures the pane and leaves the strip around it reporting the size it worked out for text
    // lying flat. Measured on the stand with a panel folded against the right edge: the strip needed 179 and its group
    // was laid out at 78, which is three tabs at the 26 of an unturned row. It corrected itself as soon as anything
    // forced another layout pass, so clicking a tab looked like a fix.
    public static readonly AdamantiumProperty LabelRotationProperty = AdamantiumProperty.Register(nameof(LabelRotation),
        typeof(PaneLabelRotation), typeof(Pane),
        new PropertyMetadata(PaneLabelRotation.None,
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsParentMeasure));

    public PaneLabelRotation LabelRotation
    {
        get => GetValue<PaneLabelRotation>(LabelRotationProperty);
        set => SetValue(LabelRotationProperty, value);
    }

    /// <summary>Whether this pane comes back with a restored layout. True for tools (part of the workspace); false for
    /// documents and for throwaway utilities, whose existence belongs to a session, not to the arrangement.</summary>
    public static readonly AdamantiumProperty RestoreProperty = AdamantiumProperty.Register(nameof(Restore),
        typeof(bool), typeof(Pane), new PropertyMetadata(true));

    public DockZone Zone
    {
        get => GetValue<DockZone>(ZoneProperty);
        set => SetValue(ZoneProperty, value);
    }

    public DockZone Allowed
    {
        get => GetValue<DockZone>(AllowedProperty);
        set => SetValue(AllowedProperty, value);
    }

    public double MinSize
    {
        get => GetValue<double>(MinSizeProperty);
        set => SetValue(MinSizeProperty, value);
    }

    public bool Restore
    {
        get => GetValue<bool>(RestoreProperty);
        set => SetValue(RestoreProperty, value);
    }

    /// <summary>Identity in a saved layout. The model refers to panes BY ID - holding the object would stop it being
    /// data, and there would be nothing left to serialise.</summary>
    public string Id { get; set; }

    /// <summary>What the application needs in order to MAKE this pane again - saved beside the id and handed back to
    /// <see cref="DockingArea.PaneRestoring"/> when a layout is loaded. Null for a pane the markup declares: it exists
    /// before any layout is read, so nothing has to recreate it.
    /// <para>A layout is a list of ids, and an id alone cannot conjure a pane that was opened by code. Without this,
    /// everything a region or a command opened was silently dropped on restore - which looks like a layout that only
    /// half works.</para></summary>
    public string RestoreKey { get; set; }

}
