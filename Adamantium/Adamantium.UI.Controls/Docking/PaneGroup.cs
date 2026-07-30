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
    /// and never less than its own tab strip needs - an area shorter than its strip has its headers clipped, and there
    /// is nothing left to grab or click. The answer comes from the panes' own policy, not from a number invented here.
    /// </summary>
    public double MinimumExtent(Panels.Orientation orientation)
    {
        var min = MinSizeAppliesAlong(orientation) ? LargestPaneMinimum() : 0;
        return System.Math.Max(min, StripExtent(orientation));
    }

    /// <summary>A pane's <see cref="Pane.MinSize"/> is its smallest useful size ALONG THE AXIS IT IS DOCKED ON, so it
    /// only answers for that axis. "The inspector is never narrower than 180" is a statement about width; letting it
    /// answer for height too meant the inspector's width forbade the console below it from being dragged taller - a
    /// limit nobody wrote and nothing explains. A group in the centre has no single axis, so its panes speak for
    /// both.</summary>
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

    /// <summary>What the tab strip needs - measured, not assumed - and only along the axis it actually stacks against.
    /// A strip across the top is a floor on HEIGHT; it says nothing about how narrow the group may be.</summary>
    private double StripExtent(Panels.Orientation orientation)
    {
        if (ItemsHostPanel is not { } panel) return 0;

        return TabStripPlacement is TabStripPlacement.Left or TabStripPlacement.Right
            ? orientation == Panels.Orientation.Horizontal ? panel.DesiredSize.Width : 0
            : orientation == Panels.Orientation.Vertical ? panel.DesiredSize.Height : 0;
    }

    public PaneGroup()
    {
        // A torn-off pane is the docking area's business, not the application's: it becomes another root of the same
        // layout. Without a handler the strip would simply put the tab back, which is exactly what it did before.
        TabTornOff += (_, e) =>
        {
            if (Area is not { } area || e.Container is not Pane pane) return;
            e.Handled = area.TearOff(pane, e);
        };

        SelectionChanged += (_, _) => SyncChrome();

        // SyncFold as well as SyncChrome: the fold pushes each pane's label rotation, and a rebuild ADDS the panes after
        // the state/edge that caused the fold was set - so a pane arriving later never learned it was in a folded strip
        // and kept measuring itself for a label lying flat. Measured: of three tabs in a folded column only the last was
        // 41x70; the first two stayed 78x29 and the three drew on top of each other.
        Items.CollectionChanged += (_, _) =>
        {
            SyncChrome();
            SyncFold();
        };

        // While folded down, the only thing left is the tab strip - so a click on it is what shows the panel. Preview, and
        // before selection: the point of the click is "show me this panel", and it should not have to be made twice.
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnPreviewDown), handledEventsToo: true);
    }

    // A click on a put-away strip REVEALS the panel - it does not pin it back. The strip stays where it is and the body
    // appears over the neighbours; only the pin button puts the panel back into the layout. That is the whole difference
    // between glancing at a tool and keeping it open, and it is why this is three states and not a flag.
    private void OnPreviewDown(object sender, MouseButtonEventArgs e)
    {
        if (State == PaneGroupState.Collapsed) Area?.Reveal(this);
    }

    // --- Tearing the PANEL off by its caption ------------------------------------------------------------------------
    // Dragging a TAB moves one pane; dragging the CAPTION moves the panel, with every pane in it. Two gestures, told
    // apart by where the press landed - not by how many tabs happen to be open, which would make the same drag mean
    // different things at different times.

    private Vector2 _captionPress;
    private bool _captionPressed;

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);

        // Handled means a child took it - the pin and close buttons do, which is why they are not torn off by a wobble.
        if (e.Handled || _caption == null) return;
        if (e.OriginalSource is not IUIComponent source || !IsWithin(source, _caption)) return;

        _captionPress = e.GetPosition(this);
        _captionPressed = true;
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (!_captionPressed) return;

        // Whether the button is still down is the device's to answer, not ours to remember - see TabItem.OnMouseMove for
        // what a latched flag does when the up lands in a tree that has been rebuilt underneath it.
        if (e.MouseDevice.LeftButton != MouseButtonState.Pressed)
        {
            _captionPressed = false;
            return;
        }

        if (!PlatformSettings.ExceedsDragThreshold(e.GetPosition(this) - _captionPress)) return;

        // Crossing the threshold IS the tear-off, exactly as it is for a tab: the panel becomes a window here and then,
        // and the platform's own move loop carries the gesture from there.
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

    // Where each tab actually ended up, and what render offset it still carries. Overlapping tabs mean either two slots
    // at one position or a leftover drag transform, and only the numbers tell which.
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        if (DockingArea.LogDocking) Log();

        return size;
    }

    /// <summary>What this group is and what its tabs are doing, printed from inside the layout pass. Set
    /// ADAMANTIUM_DOCK_LOG=1 to see it.</summary>
    private void Log()
    {
        var first = Items.OfType<Pane>().FirstOrDefault();
        var rotation = first == null ? "-" : first.LabelRotation.ToString();
        var hasHeaderTemplate = first?.HeaderTemplate != null;

        var panelKind = ItemsHostPanel is Panels.TabPanel tabPanel
            ? $"TabPanel/{tabPanel.Orientation}"
            : ItemsHostPanel?.GetType().Name ?? "none";

        var strip = GetTemplateChild("PART_TabStrip") as IMeasurableComponent;
        var stripCell = strip == null
            ? "strip=none"
            : $"strip=r{Panels.Grid.GetRow((IAdamantiumComponent)strip)}c{Panels.Grid.GetColumn((IAdamantiumComponent)strip)} at {strip.Bounds} want={strip.DesiredSize.Width:F0}x{strip.DesiredSize.Height:F0} stripValid={strip.IsMeasureValid} stripOrient={(strip as TabStripScroller)?.Orientation}" +
              (GetTemplateChild("PART_SelectionIndicator") is Base.MeasurableUIComponent ind
                  ? $" ind={ind.Width:F0}x{ind.Height:F0} at {ind.Bounds.Width:F0}x{ind.Bounds.Height:F0} hAlign={ind.HorizontalAlignment}"
                  : " ind=none");

        var text2 = "";
        if (GetTemplateChild("PART_PinButton") is Buttons.Button pin)
        {
            text2 = $" pinOver={(pin.BackgroundPointerOver == null ? "null" : "set")} pinPressed={(pin.BackgroundPressed == null ? "null" : "set")}" +
                    $" over={pin.IsMouseOver} pressed={pin.IsPressed} focusable={pin.Focusable}";
        }

        // Template identity next to panel identity: a NEW panel with the SAME template means the rebuild came from
        // somewhere other than a template change; a new template means the theme handed out a fresh instance.
        var text = $"[PaneGroup #{GetHashCode()} {Items.Count} tabs {stripCell}" +
                   $" gWant={DesiredSize.Width:F0}x{DesiredSize.Height:F0} gAt={Bounds.Width:F0}x{Bounds.Height:F0}" +
                   $" host=#{ItemsHostPanel?.GetHashCode()} panel={panelKind}" +
                   $" tmpl=#{Template?.GetHashCode()}" +
                   $" parent={VisualParent?.GetType().Name}" +
                   $" kind={Kind} edge={Edge} state={State}" +
                   $" rot={rotation} hdrTmpl={hasHeaderTemplate}]";

        for (var i = 0; i < Items.Count; i++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(i) is not TabItem tab) continue;

            var offset = tab.RenderTransform is Core.Media.Transform transform ? transform.TranslateX : 0;

            // Is this tab actually IN the strip's panel? A container that never got re-attached keeps drawing at the
            // bounds of its previous life - which looks exactly like an overlap and is nothing of the kind.
            var panel = ItemsHostPanel as Panels.Panel;
            var inChildren = panel != null && panel.Children.Contains(tab);

            // Which template the TAB carries versus which one actually reached its header presenter: a turned label that
            // never resized means one of those two links is stale, and only their identities say which.
            var ownTemplate = tab.HeaderTemplate?.GetHashCode();
            var headerHost = tab.GetTemplateChild("PART_ContentPresenter") as ContentPresenter;
            var presenterTemplate = headerHost?.ContentTemplate?.GetHashCode();
            var presenterDesired = headerHost == null ? "-" : $"{headerHost.DesiredSize.Width:F0}x{headerHost.DesiredSize.Height:F0}";

            text += $" [{i}] x={tab.Bounds.X:F0} w={tab.Bounds.Width:F0}" +
                    $" desired={tab.DesiredSize.Width:F0}x{tab.DesiredSize.Height:F0}" +
                    $" rot={(tab as Pane)?.LabelRotation} hdr=#{ownTemplate} cp=#{presenterTemplate} cpSize={presenterDesired}" +
                    $" m={(tab as Pane)?.MeasureCount} at={(tab as Pane)?.LastMeasureConstraint.Width:F0}x{(tab as Pane)?.LastMeasureConstraint.Height:F0}" +
                    $" dx={offset:F0} inChildren={inChildren} vis={tab.Visibility}";
        }

        System.Console.WriteLine(text + text2);
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

    /// <summary>Document group or tool group. A group of tools wears a header with the active pane's name and its
    /// buttons, and carries its tabs along the bottom; a group of documents wears tabs on top and nothing else.
    /// <para>Taken from WHERE THE GROUP STANDS, not from the panes in it: the document well is a document group and
    /// everything outside it is a tool group. That is what makes a tool dropped into the centre behave like a document -
    /// which it has to, because the zones for tools are the EDGES, and a panel in the centre has no edge to fold against.
    /// Reading it off the first pane instead put a caption with a pin button in the middle of the editing area.</para>
    /// <para>Pushed from the model on every rebuild, like <see cref="Edge"/>. The pane's own <see cref="Pane.Kind"/>
    /// stays what it is - it is policy (what closing means, whether it comes back with a saved layout), not looks.</para>
    /// </summary>
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

    /// <summary>Whether the panel is folded to its strip - true in BOTH unpinned states, because a revealed panel keeps
    /// its strip against the edge exactly as a put-away one does; what differs is only whether the body is showing, and
    /// the body is not in this template at all. Folded here rather than compared in the theme so a style needs one plain
    /// trigger (the <see cref="TabItem.ShowCloseButton"/> idiom).</summary>
    public static readonly AdamantiumProperty IsFoldedProperty = AdamantiumProperty.Register(nameof(IsFolded),
        typeof(bool), typeof(PaneGroup), new PropertyMetadata(false));

    public bool IsFolded
    {
        get => GetValue<bool>(IsFoldedProperty);
        private set => SetValue(IsFoldedProperty, value);
    }

    /// <summary>Whether this group is the WHOLE of a floating window. Such a panel wears no caption of its own: the
    /// window's title bar already names it, and its own header would say the same word twice - with a pin that has no
    /// edge to fold against and a close button beside the system's own.
    /// <para>Pushed from the model on every rebuild, like <see cref="Edge"/>: dock a second panel beside it inside that
    /// window and it stops being the whole of it, at which point the captions are what tell the two apart and are how
    /// each is dragged.</para></summary>
    public static readonly AdamantiumProperty IsFloatingRootProperty = AdamantiumProperty.Register(
        nameof(IsFloatingRoot), typeof(bool), typeof(PaneGroup),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure));

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

        // Which way the tabs QUEUE is the theme's to say (a trigger on IsFolded + Edge). Writing ItemsPanel from here
        // would set it LOCALLY, which outranks the theme and never gives it back - and an ItemsPanel written twice is
        // what throws the live strip away and builds another.
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
        Title = (SelectedItem as Pane)?.Header ?? Items.OfType<Pane>().FirstOrDefault()?.Header;
    }

    private ButtonBase _pinButton;
    private ButtonBase _closeButton;

    /// <summary>The caption, which is what the panel is dragged by. A PART because the gesture needs to know where it
    /// is: a press anywhere else in the group belongs to a tab or to the body.</summary>
    private IUIComponent _caption;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // A tool group's header carries the two verbs that only apply to a tool. Re-found on every template application
        // and unsubscribed first: a placement change swaps the whole template, and a handler left on a discarded button
        // is a click that goes nowhere anyone can see.
        if (_pinButton != null) _pinButton.Click -= OnPinClicked;
        if (_closeButton != null) _closeButton.Click -= OnCloseClicked;

        _pinButton = GetTemplateChild("PART_PinButton") as ButtonBase;
        _closeButton = GetTemplateChild("PART_CloseButton") as ButtonBase;
        _caption = GetTemplateChild("PART_Header") as IUIComponent;

        if (_pinButton != null) _pinButton.Click += OnPinClicked;
        if (_closeButton != null) _closeButton.Click += OnCloseClicked;

        if (DockingArea.LogDocking && _pinButton != null)
        {
            _pinButton.PropertyChanged += (_, e) =>
            {
                if (e.Property?.Name is not ("IsMouseOver" or "IsPressed")) return;
                var inner = _pinButton?.GetTemplateChild("InnerBorder") as Decorators.Border;
                var fill = inner?.Background is Core.Media.SolidColorBrush b ? b.Color.ToString() : inner?.Background?.GetType().Name ?? "null";
                System.Console.WriteLine($"[PIN] {e.Property.Name}={e.NewValue} inner={inner?.GetType().Name} fill={fill}");
            };
        }

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
