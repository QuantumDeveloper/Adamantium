using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// Shows its <see cref="Child"/> on a window-wide overlay layer (NOT a separate OS window): it never leaves the parent
/// window's bounds. The child is positioned relative to <see cref="PlacementTarget"/> via <see cref="Placement"/> +
/// offsets, re-evaluated every frame, so it FOLLOWS a moving target (e.g. a value tooltip riding a slider thumb). The
/// popup occupies no space where it is declared - it is a portal into the window's <see cref="PopupLayer"/>.
/// </summary>
public class Popup : MeasurableUIComponent, IContainer
{
    public static readonly AdamantiumProperty IsOpenProperty = AdamantiumProperty.Register(nameof(IsOpen),
        typeof(bool), typeof(Popup), new PropertyMetadata(false, OnIsOpenChanged));

    public static readonly AdamantiumProperty ChildProperty = AdamantiumProperty.Register(nameof(Child),
        typeof(IMeasurableComponent), typeof(Popup), new PropertyMetadata(null, OnChildChanged));

    public static readonly AdamantiumProperty ChildTemplateProperty = AdamantiumProperty.Register(nameof(ChildTemplate),
        typeof(ControlTemplate), typeof(Popup), new PropertyMetadata(null));

    public static readonly AdamantiumProperty PlacementTargetProperty = AdamantiumProperty.Register(nameof(PlacementTarget),
        typeof(UIComponent), typeof(Popup), new PropertyMetadata(null));

    public static readonly AdamantiumProperty PlacementProperty = AdamantiumProperty.Register(nameof(Placement),
        typeof(PlacementMode), typeof(Popup), new PropertyMetadata(PlacementMode.Bottom));

    public static readonly AdamantiumProperty HorizontalOffsetProperty = AdamantiumProperty.Register(nameof(HorizontalOffset),
        typeof(double), typeof(Popup), new PropertyMetadata(0.0));

    public static readonly AdamantiumProperty VerticalOffsetProperty = AdamantiumProperty.Register(nameof(VerticalOffset),
        typeof(double), typeof(Popup), new PropertyMetadata(0.0));

    public static readonly AdamantiumProperty FlipToFitProperty = AdamantiumProperty.Register(nameof(FlipToFit),
        typeof(bool), typeof(Popup), new PropertyMetadata(false));

    public static readonly AdamantiumProperty DockEdgeProperty = AdamantiumProperty.Register(nameof(DockEdge),
        typeof(Dock?), typeof(Popup), new PropertyMetadata(null));

    public static readonly AdamantiumProperty DockFillProperty = AdamantiumProperty.Register(nameof(DockFill),
        typeof(bool), typeof(Popup), new PropertyMetadata(false));

    public static readonly AdamantiumProperty FillWindowProperty = AdamantiumProperty.Register(nameof(FillWindow),
        typeof(bool), typeof(Popup), new PropertyMetadata(false));

    private IPopupHost _host;   // the window layer this popup is registered with while open

    public bool IsOpen
    {
        get => GetValue<bool>(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    [Content]
    public IMeasurableComponent Child
    {
        get => GetValue<IMeasurableComponent>(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    /// <summary>A template for the popup's <see cref="Child"/>, built LAZILY on the first <see cref="Open"/> instead of
    /// eagerly with the owner's template. For heavy content that is pointless to build until shown (a menu submenu's card +
    /// items host, otherwise built + themed inside every MenuItem, even leaves that never open). Ignored once Child is set.</summary>
    public ControlTemplate ChildTemplate
    {
        get => GetValue<ControlTemplate>(ChildTemplateProperty);
        set => SetValue(ChildTemplateProperty, value);
    }

    /// <summary>Raised right after <see cref="ChildTemplate"/> is built into <see cref="Child"/> (once, on first open) so the
    /// owner can wire up named parts inside the freshly built content (e.g. a MenuItem connecting its ItemsPresenter).</summary>
    public event EventHandler ContentBuilt;

    private TemplateResult _deferredContent;

    /// <summary>Finds a named element inside the lazily-built <see cref="ChildTemplate"/> content (null before first open).</summary>
    internal IAdamantiumComponent FindContentChild(string name) => _deferredContent?.GetComponentByName(name);

    /// <summary>The element the popup positions against. Defaults to the element the popup is declared under.</summary>
    public UIComponent PlacementTarget
    {
        get => GetValue<UIComponent>(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    public PlacementMode Placement
    {
        get => GetValue<PlacementMode>(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public double HorizontalOffset
    {
        get => GetValue<double>(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    public double VerticalOffset
    {
        get => GetValue<double>(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <summary>When the popup would overflow the window past the target on the placement axis, flip to the opposite side
    /// if that fits better (a Bottom popup opens above when there's not enough room below). Default false (clamp only).</summary>
    public bool FlipToFit
    {
        get => GetValue<bool>(FlipToFitProperty);
        set => SetValue(FlipToFitProperty, value);
    }

    /// <summary>When set, the child is docked to that EDGE OF THE WINDOW (not positioned against a target): pinned to the
    /// edge on the perpendicular axis, and along the edge either stretched to the window (child alignment = Stretch on the
    /// cross axis) or sized to its content and aligned by the child's Horizontal/VerticalAlignment. Used by SlidePanel.</summary>
    public Dock? DockEdge
    {
        get => GetValue<Dock?>(DockEdgeProperty);
        set => SetValue(DockEdgeProperty, value);
    }

    /// <summary>When edge-docked, also fill the MAIN (thickness) axis with the window - a full-window panel. Default
    /// false (the main axis is the child's content/explicit size). The cross axis always fills the window.</summary>
    public bool DockFill
    {
        get => GetValue<bool>(DockFillProperty);
        set => SetValue(DockFillProperty, value);
    }

    /// <summary>Fill the ENTIRE window (both axes) at the origin - a full-window overlay (a modal scrim / backdrop). Unlike
    /// <see cref="DockEdge"/> this carries no edge semantics; it just covers the window. Takes precedence over DockEdge.</summary>
    public bool FillWindow
    {
        get => GetValue<bool>(FillWindowProperty);
        set => SetValue(FillWindowProperty, value);
    }

    /// <summary>Explicit target, else the element this popup is declared under.</summary>
    internal UIComponent EffectiveTarget => PlacementTarget ?? VisualParent as UIComponent;

    // A popup occupies NO space in its own parent - its child lives in the window's popup layer.
    protected override Size MeasureOverride(Size availableSize) => Size.Zero;
    protected override Size ArrangeOverride(Size finalSize) => Size.Zero;

    private static void OnIsOpenChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var popup = (Popup)a;
        if ((bool)e.NewValue) popup.Open(); else popup.Close();
    }

    private static void OnChildChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var popup = (Popup)a;
        // Logical child only (for DataContext inheritance) - NOT a visual child, so the main layout/render never draws
        // it in place; the popup layer measures/arranges/renders it in the overlay.
        if (e.OldValue is IMeasurableComponent oldChild) popup.LogicalChildrenCollection.Remove(oldChild);
        if (e.NewValue is IMeasurableComponent newChild) popup.LogicalChildrenCollection.Add(newChild);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsOpen) Open();   // IsOpen may have been set before the popup entered the tree
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Close();
        base.OnDetachedFromVisualTree(e);
    }

    // A popup's hosted CHILD is a logical-only child rendered detached on the overlay - it has NO visual path back to the
    // window. So a popup opened from WITHIN an overlay (a submenu inside an open menu) can't find the window by walking its
    // anchor's visual ancestors. Record each hosted overlay root -> its window here; a nested popup then finds the SAME
    // window via the recorded entry on its anchor's overlay-root ancestor. Weak-keyed so a closed popup's child is collectable.
    private static readonly ConditionalWeakTable<IUIComponent, IPopupHost> OverlayRootHost = new();

    private void Open()
    {
        // First open: realize the deferred content (build + theme it NOW, not in the owner's template). A leaf whose submenu
        // never opens never runs this, which is the whole point - it drops the submenu card/items-host cost off every row.
        if (Child == null && ChildTemplate != null)
        {
            _deferredContent = ChildTemplate.Build(this);
            if (_deferredContent?.RootComponent is IMeasurableComponent built)
            {
                Child = built;   // OnChildChanged logical-parents (and themes) it
                ContentBuilt?.Invoke(this, EventArgs.Empty);
            }
        }
        if (_host != null || Child == null) return;
        // Find the window (popup host) from the TARGET's tree, falling back to the popup's own. A declared popup finds it by
        // walking visual ancestors; a popup opened inside an overlay (a submenu) has no such path, so also consult the host
        // recorded on each overlay root when it was hosted (below). The target is always the anchor we position against.
        IUIComponent anchor = EffectiveTarget ?? this;
        _host = FindPopupHost(anchor);
        if (_host == null) return;   // not in a window yet; OnAttachedToVisualTree retries
        if (Child is UIComponent child) child.DataContext = DataContext;
        if (Child is IUIComponent overlayRoot) OverlayRootHost.AddOrUpdate(overlayRoot, _host);   // so nested popups find it
        _host.PopupLayer.Add(this);
    }

    private void Close()
    {
        if (Child is IUIComponent overlayRoot) OverlayRootHost.Remove(overlayRoot);
        _host?.PopupLayer.Remove(this);
        _host = null;
    }

    // Also used by a menu to find its window (for capping submenu scroll height) - its overlay rows have no visual path back.
    internal static IPopupHost FindPopupHost(IUIComponent anchor)
    {
        for (var node = anchor; node != null; node = node.VisualParent)
        {
            if (node is IPopupHost host) return host;                            // in the main window tree
            if (OverlayRootHost.TryGetValue(node, out var recorded)) return recorded;   // inside an already-open overlay
        }
        return null;
    }

    /// <summary>True if <paramref name="node"/> sits inside an open popup hosted by <paramref name="host"/> (any card in the
    /// menu's own overlay tree). Lets a menu treat a press on a submenu's chrome - a scroll arrow, the card padding - as
    /// "inside", not a light-dismiss, even though that chrome is neither a MenuItem nor the root popup's own card.</summary>
    internal static bool IsWithinOverlayOf(IUIComponent node, IPopupHost host)
    {
        if (host == null) return false;
        for (var n = node; n != null; n = n.VisualParent)
            if (OverlayRootHost.TryGetValue(n, out var h) && ReferenceEquals(h, host)) return true;
        return false;
    }

    /// <summary>A MaxHeight that keeps a scrollable flyout (a long menu) inside <paramref name="host"/>'s window with a small
    /// margin - so it scrolls rather than clipping; unbounded when the window size isn't known yet.</summary>
    internal static double WindowHeightCap(IPopupHost host)
    {
        var windowHeight = host?.PopupLayer?.WindowSize.Height ?? 0;
        return windowHeight > 24 ? windowHeight - 24 : double.PositiveInfinity;
    }

    // IContainer: the AUML loader sets Child via the [Content] property.
    public void AddOrSetChildComponent(object component) { if (component is IMeasurableComponent c) Child = c; }
    public void RemoveAllChildComponents() => Child = null;
    public IReadOnlyList<object> GetChildComponents() => Child != null ? [Child] : [];
    public void InsertChildComponent(int index, object component) { if (component is IMeasurableComponent c) Child = c; }
    public void RemoveChildComponentAt(int index) => Child = null;
}
