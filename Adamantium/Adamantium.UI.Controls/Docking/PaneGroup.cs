using System.Linq;
using Adamantium.UI.Core;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// An area showing several <see cref="Pane"/>s as tabs - the LEAF of a docking layout. Splits nest; groups never do.
/// <para>A <see cref="TabControl"/> already is this: tabs, reordering along the strip, and tearing a tab off into its
/// own window all work there and were built for exactly this. A group adds only where it sits.</para>
/// </summary>
public class PaneGroup : TabControl, Panels.IPaneMinimum
{
    /// <summary>How small this group may become along one axis: the largest <see cref="Pane.MinSize"/> among its panes,
    /// and never less than its own chrome - an area shorter than that has nothing left to grab or click.</summary>
    public double MinimumExtent(Panels.Orientation orientation)
    {
        var min = MinSizeAppliesAlong(orientation) ? LargestPaneMinimum() : 0;
        min = System.Math.Max(min, StripExtent(orientation));

        // The DOCUMENT area has a floor of its own along both axes (rule 7.6): it pays for every tool docked against
        // it, and its panes cannot state this - documents come and go, the centre outlives all of them.
        if (Kind == PaneKind.Document && Area is { } area) min = System.Math.Max(min, area.DocumentMinSize);

        return min;
    }

    // A pane's MinSize is its smallest useful size ALONG THE AXIS IT IS DOCKED ON. Letting it answer for both meant the
    // inspector's width forbade the console below from being dragged taller. A centre group has no single axis.
    private bool MinSizeAppliesAlong(Panels.Orientation orientation)
    {
        return Zone switch
        {
            DockZone.Left or DockZone.Right => orientation == Panels.Orientation.Horizontal,
            DockZone.Top or DockZone.Bottom => orientation == Panels.Orientation.Vertical,
            _ => true
        };
    }

    private double LargestPaneMinimum()
    {
        var min = 0.0;
        for (var i = 0; i < Items.Count; i++)
        {
            // Authored panes ARE the items; bound ones are reached through their container.
            if (Items[i] is Pane authored) min = System.Math.Max(min, authored.MinSize);
            else if (ItemContainerGenerator.ContainerFromIndex(i) is Pane generated) min = System.Math.Max(min, generated.MinSize);
        }
        return min;
    }

    // What the panel's CHROME needs - measured, not assumed - along the axis it stacks against: the tab strip, plus the
    // caption a tool wears above its body. Measured with the strip alone, a squeezed console got 27px and its caption
    // drew past the bottom of the layout, which looks like a panel sliding off the window.
    private double StripExtent(Panels.Orientation orientation)
    {
        var strip = 0.0;
        if (ItemsHostPanel is { } panel)
        {
            strip = TabStripPlacement is TabStripPlacement.Left or TabStripPlacement.Right
                ? orientation == Panels.Orientation.Horizontal ? panel.DesiredSize.Width : 0
                : orientation == Panels.Orientation.Vertical ? panel.DesiredSize.Height : 0;
        }

        // A caption along the top is a floor on HEIGHT - it sits across the body, not along it.
        if (orientation == Panels.Orientation.Vertical
            && _caption is IMeasurableComponent { Visibility: Visibility.Visible } caption)
        {
            strip += caption.DesiredSize.Height;
        }

        return strip;
    }

    public PaneGroup()
    {
        // A torn-off pane becomes another ROOT of this layout - the area's business, not the application's. Without a
        // handler the strip simply puts the tab back.
        TabTornOff += (_, e) =>
        {
            if (Area is not { } area || e.Container is not Pane pane) return;
            e.Handled = area.TearOff(pane, e);
        };

        SelectionChanged += (_, _) =>
        {

            SyncChrome();
        };

        // Which host holds the body follows the CONTENT as well as the state - the selected pane changes under both.
        PropertyChanged += (_, e) =>
        {
            if (e.Property == SelectedContentProperty) SyncContentHost();
        };

        // SyncFold too: a rebuild ADDS panes after the state that caused the fold was set, so a pane arriving later
        // never learned it was in a folded strip and kept measuring for a label lying flat (measured: of three tabs,
        // only the last carried the turned footprint and all three drew on top of each other).
        Items.CollectionChanged += (_, _) =>
        {
            SyncChrome();
            SyncFold();
        };

        // While folded down, the only thing left is the tab strip - so a click on it is what shows the panel. Preview, and
        // before selection: the point of the click is "show me this panel", and it should not have to be made twice.
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnPreviewDown), handledEventsToo: true);
    }

    // A click on a put-away strip REVEALS the panel; only the pin puts it back. Glancing at a tool and keeping it open
    // are different things, which is why this is three states and not a flag.
    private void OnPreviewDown(object sender, MouseButtonEventArgs e)
    {
        // Touching a panel is what makes it the one being worked in - anywhere in it, tab strip, caption or body.
        Area?.MakeActive(this);

        if (State == PaneGroupState.Collapsed) Area?.Reveal(this);
    }

    /// <summary>The panel being worked in - ONE across the whole layout, floating windows included. Only its border is
    /// drawn in the accent, which is the only thing on screen saying where a keystroke or a newly opened pane will go.
    /// <para>Set by the docking area, never authored: which panel is active is a fact about the session.</para></summary>
    public static readonly AdamantiumProperty IsActiveProperty = AdamantiumProperty.Register(
        nameof(IsActive), typeof(bool), typeof(PaneGroup), new PropertyMetadata(false));

    public bool IsActive
    {
        get => GetValue<bool>(IsActiveProperty);
        internal set => SetValue(IsActiveProperty, value);
    }

    // --- Tearing the PANEL off by its caption -----------------------------------------------------------------------
    // A TAB drag moves one pane, a CAPTION drag moves the panel with every pane in it - told apart by where the press
    // landed, not by how many tabs happen to be open.

    private Vector2 _captionPress;
    private bool _captionPressed;

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);

        // Handled means a child took it - the pin and close buttons do, which is why they are not torn off by a wobble.
        if (e.Handled) return;
        if (e.OriginalSource is not IUIComponent source) return;

        // EITHER caption: the docked one, or the flyout's while the panel is revealed.
        var onCaption = (_caption != null && IsWithin(source, _caption))
                        || (_flyoutCaption is IUIComponent flyout && IsWithin(source, flyout));

        if (!onCaption) return;

        _captionPress = e.GetPosition(this);
        _captionPressed = true;
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (!_captionPressed) return;

        // Whether the button is down is the DEVICE's to answer: a latched flag never clears when the up lands in a tree
        // that has been rebuilt underneath it (see TabItem.OnMouseMove).
        if (e.MouseDevice.LeftButton != MouseButtonState.Pressed)
        {
            _captionPressed = false;
            return;
        }

        if (!PlatformSettings.ExceedsDragThreshold(e.GetPosition(this) - _captionPress)) return;

        // Crossing the threshold IS the tear-off, as for a tab: the platform's move loop carries it from here.
        _captionPressed = false;
        Area?.TearOffGroup(this, Mouse.ScreenCoordinates);
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        _captionPressed = false;
    }

    private static bool IsWithin(IUIComponent node, IUIComponent ancestor)
    {
        for (var n = node; n != null; n = n.VisualParent)
        {
            if (ReferenceEquals(n, ancestor)) return true;
        }
        return false;
    }


    /// <summary>The docking area this group lives in, or null when it is used as a plain tab control.</summary>
    private DockingArea Area
    {
        get
        {
            for (var parent = VisualParent; parent != null; parent = parent.VisualParent)
            {
                if (parent is DockingArea area) return area;
            }
            return null;
        }
    }

    /// <summary>Document group or tool group: a tool wears a caption and keeps its tabs at the bottom, a document wears
    /// tabs on top and nothing else. Taken from WHERE THE GROUP STANDS (rule 1.2), not from the panes in it - reading
    /// it off the first pane put a caption with a pin in the middle of the editing area. The pane's own
    /// <see cref="Pane.Kind"/> is unaffected: that is policy, not looks.</summary>
    public static readonly AdamantiumProperty KindProperty = AdamantiumProperty.Register(nameof(Kind),
        typeof(PaneKind), typeof(PaneGroup), new PropertyMetadata(PaneKind.Document));

    public PaneKind Kind
    {
        get => GetValue<PaneKind>(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Docked, put away, or being looked at (see <see cref="PaneGroupState"/>). Set from the model by
    /// <see cref="DockingArea"/> - which of the three a panel is in is part of a layout, not a state the control keeps to
    /// itself.</summary>
    public static readonly AdamantiumProperty StateProperty = AdamantiumProperty.Register(nameof(State),
        typeof(PaneGroupState), typeof(PaneGroup),
        new PropertyMetadata(PaneGroupState.Docked, PropertyMetadataOptions.AffectsMeasure, OnFoldChanged));

    public PaneGroupState State
    {
        get => GetValue<PaneGroupState>(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Whether the panel is folded to its strip - true in BOTH unpinned states: a revealed panel keeps its
    /// strip against the edge exactly as a put-away one does. Folded here so a style needs one plain trigger.</summary>
    public static readonly AdamantiumProperty IsFoldedProperty = AdamantiumProperty.Register(nameof(IsFolded),
        typeof(bool), typeof(PaneGroup), new PropertyMetadata(false));

    public bool IsFolded
    {
        get => GetValue<bool>(IsFoldedProperty);
        private set => SetValue(IsFoldedProperty, value);
    }

    /// <summary>Whether this group is the WHOLE of a floating window. Such a panel wears no caption: the title bar
    /// already names it, and a pin there would have no edge to fold against. Dock a second panel into that window and
    /// it stops being the whole of it - the captions are then what tell the two apart.</summary>
    public static readonly AdamantiumProperty IsFloatingRootProperty = AdamantiumProperty.Register(
        nameof(IsFloatingRoot), typeof(bool), typeof(PaneGroup),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure, OnFloatingRootChanged));

    // Being the whole of a window (or ceasing to be) decides whether the strip is drawn at all - and it changes without
    // the panes changing, when something else is docked into that window beside this panel.
    private static void OnFloatingRootChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        => (a as PaneGroup)?.SyncChrome();

    public bool IsFloatingRoot
    {
        get => GetValue<bool>(IsFloatingRootProperty);
        set => SetValue(IsFloatingRootProperty, value);
    }

    /// <summary>Which side of the layout this group sits against, or <see cref="DockZone.None"/> in the middle. Pushed
    /// from the model on every rebuild, because it is read from where the group actually IS - the authored
    /// <see cref="Zone"/> only says where it started.</summary>
    public static readonly AdamantiumProperty EdgeProperty = AdamantiumProperty.Register(nameof(Edge),
        typeof(DockZone), typeof(PaneGroup),
        new PropertyMetadata(DockZone.None, PropertyMetadataOptions.AffectsMeasure, OnFoldChanged));

    public DockZone Edge
    {
        get => GetValue<DockZone>(EdgeProperty);
        set => SetValue(EdgeProperty, value);
    }

    private static void OnFoldChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        => (a as PaneGroup)?.SyncFold();

    // How the tabs are turned follows from two things and nothing else: whether the group is folded down, and which
    // edge it is folded against. A panel collapsed on a SIDE is a narrow column - its labels lie on their side and face
    // out of it - while one collapsed at the top or bottom is already a wide band and needs nothing done to it.
    private void SyncFold()
    {
        // The base ctor seeds every property with its default and fires the changed callback, so this can run before
        // ItemsControl's ctor has made the collection. Nothing to fold yet.
        if (Items == null) return;

        IsFolded = State != PaneGroupState.Docked;

        // PUT AWAY means nothing is showing, so nothing is selected: the strip is a row of buttons and a highlighted one
        // would claim a panel is open when none is. A TabControl otherwise insists on a selection and hands it to the first
        // tab, which is how the highlight jumped to "Inspector" the moment the panel folded. Revealing selects the tab that
        // was pressed; pinning restores a normal strip, which picks one again by itself.
        RequiresSelection = State != PaneGroupState.Collapsed;
        if (State == PaneGroupState.Collapsed) SelectedIndex = -1;

        SyncContentHost();

        var rotation = IsFolded
            ? Edge switch
            {
                DockZone.Left => PaneLabelRotation.Left,
                DockZone.Right => PaneLabelRotation.Right,
                _ => PaneLabelRotation.None
            }
            : PaneLabelRotation.None;

        foreach (var pane in Items.OfType<Pane>())
        {
            pane.LabelRotation = rotation;
        }

        // The flyout follows the state, and it is driven from HERE rather than by a trigger in the template. A trigger
        // has to be UNDONE to close the popup, and undoing it went through ClearValue - which leaves the property Unset,
        // so the next reader (GetValue<bool>) threw and took the rest of this method with it. Measured: the panel docked
        // with its tab labels still turned on their side, because the line above never ran.
        // LAST in this method, after everything that must happen whatever the popup does.
        if (_flyout != null)
        {
            _flyout.IsOpen = State == PaneGroupState.Revealed;
        }

        // Which way the tabs QUEUE is the theme's to say (a trigger on IsFolded + Edge). Writing ItemsPanel from here
        // would set it LOCALLY, which outranks the theme and never gives it back - and an ItemsPanel written twice is
        // what throws the live strip away and builds another.
    }

    /// <summary>How far the flyout of a REVEALED panel reaches across its edge, in pixels - the room the panel is worth
    /// docked. Pushed from the model, where that number lives (<see cref="PaneGroupNode.RestoreLength"/>); a star length
    /// is turned into pixels against the docking area, because a flyout is placed, not shared out.
    /// <para>Only the flyout uses it. In the tree a revealed panel is still just its strip - it draws OVER its
    /// neighbours rather than pushing them aside (rule 3.10).</para></summary>
    public static readonly AdamantiumProperty RevealExtentProperty = AdamantiumProperty.Register(nameof(RevealExtent),
        typeof(double), typeof(PaneGroup), new PropertyMetadata(240.0));

    public double RevealExtent
    {
        get => GetValue<double>(RevealExtentProperty);
        set => SetValue(RevealExtentProperty, value);
    }

    // WHERE the active pane's content is hosted - in the panel's own body, or in the flyout. Exactly one of them holds it
    // at a time, because one piece of content belongs to one tree: a presenter goes on holding its child even when the
    // body around it is hidden, so handing the same element to the flyout would give it a second parent.
    // Two properties rather than a trigger that nulls the presenter's Content: a local write would outrank the template
    // binding and never give it back.

    public static readonly AdamantiumProperty DockedContentProperty = AdamantiumProperty.Register(nameof(DockedContent),
        typeof(object), typeof(PaneGroup), new PropertyMetadata(null));

    /// <summary>The active pane's content while the panel is DOCKED (or put away, where nothing shows it) - null while it
    /// is revealed, because the flyout has it then.</summary>
    public object DockedContent
    {
        get => GetValue(DockedContentProperty);
        private set => SetValue(DockedContentProperty, value);
    }

    public static readonly AdamantiumProperty FlyoutContentProperty = AdamantiumProperty.Register(nameof(FlyoutContent),
        typeof(object), typeof(PaneGroup), new PropertyMetadata(null));

    /// <summary>The active pane's content while the panel is REVEALED, and null at every other time.</summary>
    public object FlyoutContent
    {
        get => GetValue(FlyoutContentProperty);
        private set => SetValue(FlyoutContentProperty, value);
    }

    private void SyncContentHost()
    {
        var revealed = State == PaneGroupState.Revealed;

        // The old host is emptied BEFORE the new one is filled: the other order gives the content two parents for as
        // long as it takes the next line to run.
        if (revealed)
        {
            DockedContent = null;
            FlyoutContent = SelectedContent;
        }
        else
        {
            FlyoutContent = null;
            DockedContent = SelectedContent;
        }
    }

    /// <summary>How long the flyout runs ALONG its edge - the length of the EDGE, not of this strip. A strip is as long
    /// as its own few tab captions; the panel behind it is a panel. Pushed by the docking area, which is what knows how
    /// long an edge is.</summary>
    public static readonly AdamantiumProperty RevealLengthProperty = AdamantiumProperty.Register(nameof(RevealLength),
        typeof(double), typeof(PaneGroup), new PropertyMetadata(0.0));

    public double RevealLength
    {
        get => GetValue<double>(RevealLengthProperty);
        set => SetValue(RevealLengthProperty, value);
    }

    /// <summary>Where the flyout sits relative to this strip's top-left corner - both axes, in pixels.
    /// <para>The popup is placed <see cref="PlacementMode.Relative"/> to the strip and moved by these, rather than by one
    /// of the named placements: those CENTRE the popup on its target (right for a tooltip, which is what they were built
    /// for), and a flyout seven times wider than the strip it belongs to ended up half a window to the left of it. The
    /// docking area knows exactly where the panel should appear, so it says so outright.</para></summary>
    public static readonly AdamantiumProperty RevealOffsetXProperty = AdamantiumProperty.Register(nameof(RevealOffsetX),
        typeof(double), typeof(PaneGroup), new PropertyMetadata(0.0));

    public double RevealOffsetX
    {
        get => GetValue<double>(RevealOffsetXProperty);
        set => SetValue(RevealOffsetXProperty, value);
    }

    public static readonly AdamantiumProperty RevealOffsetYProperty = AdamantiumProperty.Register(nameof(RevealOffsetY),
        typeof(double), typeof(PaneGroup), new PropertyMetadata(0.0));

    public double RevealOffsetY
    {
        get => GetValue<double>(RevealOffsetYProperty);
        set => SetValue(RevealOffsetYProperty, value);
    }

    /// <summary>What the header shows: the active pane's own header. One title, not a list - the tabs below already say
    /// what else is here.</summary>
    public static readonly AdamantiumProperty TitleProperty = AdamantiumProperty.Register(nameof(Title),
        typeof(object), typeof(PaneGroup), new PropertyMetadata(null));

    public object Title
    {
        get => GetValue(TitleProperty);
        private set => SetValue(TitleProperty, value);
    }

    // What the caption says, derived from the panes - so nothing has to remember to update it and it cannot disagree with
    // what is actually in the group. Kind is NOT here: it comes from where the group stands (see KindProperty).
    private void SyncChrome()
    {
        // A property callback can fire while the control is still being CONSTRUCTED - default values are applied before
        // the items collection exists - so there is nothing to describe yet. The rebuild that fills the group calls
        // this again.
        if (Items == null) return;

        Title = (SelectedItem as Pane)?.Header ?? Items.OfType<Pane>().FirstOrDefault()?.Header;

        // A window showing ONE panel has nothing to choose between, and a strip of one tab is a row of buttons that all
        // do what is already done - it only takes room away from the thing being looked at. The window's title bar
        // names it instead. Drop a second panel in and the strip comes back, because then there is a choice to make.
        // NO tabs at all is the emptied document area, which holds its place without drawing a bar of nothing.
        ShowTabStrip = Items.Count > 0 && (!IsFloatingRoot || Items.Count > 1);
    }

    private ButtonBase _pinButton;
    private ButtonBase _closeButton;
    private ButtonBase _flyoutPinButton;
    private ButtonBase _flyoutCloseButton;
    private Popup _flyout;

    /// <summary>The flyout light-dismissed itself (a press outside it), which for a revealed panel means "put it away".
    /// <para>The popup owns this rather than the docking area: the flyout lives in the window's popup layer, OUTSIDE this
    /// group's own subtree, so an area-level "was the press inside the group?" test would count a press on the panel's
    /// own body as a press elsewhere and shut it the moment it was used.</para>
    /// <para>The local IsOpen the popup wrote is cleared, or it would outrank the template trigger and the panel could
    /// never be revealed again.</para></summary>
    private void OnFlyoutPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property != Popup.IsOpenProperty || _flyout is not { IsOpen: false }) return;
        if (State != PaneGroupState.Revealed) return;

        // Just put it away: the state is what drives IsOpen (see SyncFold), so nothing here has to undo the popup's own
        // write - putting the panel away is the whole answer.
        Area?.Hide(this);
    }

    /// <summary>The caption, which is what the panel is dragged by. A PART because the gesture needs to know where it
    /// is: a press anywhere else in the group belongs to a tab or to the body.</summary>
    private IUIComponent _caption;

    /// <summary>The flyout's caption - the only one a REVEALED panel has on screen, and therefore the one it is dragged
    /// by in that state.</summary>
    private IInputComponent _flyoutCaption;

    private MouseButtonEventHandler _captionDown;
    private MouseEventHandler _captionMove;
    private MouseButtonEventHandler _captionUp;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // A tool group's header carries the two verbs that only apply to a tool. Re-found on every template application
        // and unsubscribed first: a placement change swaps the whole template, and a handler left on a discarded button
        // is a click that goes nowhere anyone can see.
        if (_pinButton != null) _pinButton.Click -= OnPinClicked;
        if (_closeButton != null) _closeButton.Click -= OnCloseClicked;
        if (_flyoutPinButton != null) _flyoutPinButton.Click -= OnPinClicked;
        if (_flyoutCloseButton != null) _flyoutCloseButton.Click -= OnCloseClicked;

        _pinButton = GetTemplateChild("PART_PinButton") as ButtonBase;
        _closeButton = GetTemplateChild("PART_CloseButton") as ButtonBase;
        _caption = GetTemplateChild("PART_Header") as IUIComponent;

        // The flyout's caption drags the panel too - while a panel is revealed that is the ONLY caption it has on
        // screen, so without this a revealed panel could not be taken anywhere. Handlers go on the element itself
        // rather than relying on the press reaching this control: the flyout lives in the window's popup layer, not
        // under this group, so nothing bubbles from it to here.
        if (_flyoutCaption != null)
        {
            _flyoutCaption.RemoveHandler(Mouse.MouseDownEvent, _captionDown);
            _flyoutCaption.RemoveHandler(Mouse.MouseMoveEvent, _captionMove);
            _flyoutCaption.RemoveHandler(Mouse.MouseUpEvent, _captionUp);
        }

        _flyoutCaption = GetTemplateChild("PART_FlyoutHeader") as IInputComponent;

        if (_flyoutCaption != null)
        {
            _captionDown ??= OnMouseLeftButtonDown;
            _captionMove ??= OnMouseMove;
            _captionUp ??= OnMouseLeftButtonUp;

            _flyoutCaption.AddHandler(Mouse.MouseDownEvent, _captionDown);
            _flyoutCaption.AddHandler(Mouse.MouseMoveEvent, _captionMove);
            _flyoutCaption.AddHandler(Mouse.MouseUpEvent, _captionUp);
        }

        // The flyout carries its own pair of them: while a panel is revealed its docked caption is not on screen, and a
        // flyout you cannot pin is a panel you can only look at.
        _flyoutPinButton = GetTemplateChild("PART_FlyoutPinButton") as ButtonBase;
        _flyoutCloseButton = GetTemplateChild("PART_FlyoutCloseButton") as ButtonBase;

        if (_flyout != null)
        {
            _flyout.PropertyChanged -= OnFlyoutPropertyChanged;
        }

        _flyout = GetTemplateChild("PART_RevealFlyout") as Popup;

        if (_flyout != null)
        {
            _flyout.PropertyChanged += OnFlyoutPropertyChanged;
        }

        if (_pinButton != null) _pinButton.Click += OnPinClicked;
        if (_closeButton != null) _closeButton.Click += OnCloseClicked;
        if (_flyoutPinButton != null) _flyoutPinButton.Click += OnPinClicked;
        if (_flyoutCloseButton != null) _flyoutCloseButton.Click += OnCloseClicked;

        SyncChrome();
    }

    /// <summary>Pin: a docked group folds away to the edge it sits on, leaving its panes as buttons on that edge's strip;
    /// a folded one - whether put away or merely being looked at - comes back into the layout, tabs and all. The area owns
    /// the move, because which state a panel is in belongs to the layout.</summary>
    private void OnPinClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Area?.TogglePinned(this);
    }

    /// <summary>Close: the ACTIVE pane goes, not the group - the group is only where it was sitting, and it disappears
    /// by itself once its last pane has gone.</summary>
    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (SelectedItem is Pane pane) RequestClose(pane);
    }

    /// <summary>In a docking area the LAYOUT is the truth and this strip is a view of it, so a close goes through the
    /// model and the tabs follow on the next rebuild. What closing MEANS also differs by pane: a document goes, a tool is
    /// put away and can be brought back. Used as a plain tab control (no area above it), it behaves as one.</summary>
    protected override bool RemoveOnClose(TabItem tab, int index)
    {
        if (Area is not { } area || tab is not Pane pane) return true;

        area.ClosePane(pane);
        return false;
    }

    /// <summary>Where the AUTHOR put this group. This is the whole vocabulary of markup - a zone, not a share of a
    /// split the author cannot see. <see cref="DockingArea"/> builds the split tree from these.</summary>
    public static readonly AdamantiumProperty ZoneProperty = AdamantiumProperty.Register(nameof(Zone),
        typeof(DockZone), typeof(PaneGroup), new PropertyMetadata(DockZone.Center));

    /// <summary>Starting size in PIXELS along the zone's axis, or NaN. A hint for the first layout only: the author
    /// says "the inspector starts about 220 wide" without knowing the window size, and the first arrange turns it into
    /// a fraction. After that the fraction is the truth - a pixel number would be a lie the moment a divider moves.</summary>
    public static readonly AdamantiumProperty SizeProperty = AdamantiumProperty.Register(nameof(Size),
        typeof(double), typeof(PaneGroup), new PropertyMetadata(double.NaN));

    public DockZone Zone
    {
        get => GetValue<DockZone>(ZoneProperty);
        set => SetValue(ZoneProperty, value);
    }

    public double Size
    {
        get => GetValue<double>(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}
