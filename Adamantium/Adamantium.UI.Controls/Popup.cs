using System.Linq;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

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

    private void Open()
    {
        if (_host != null || Child == null) return;
        // Find the window (popup host) via the TARGET's tree, falling back to the popup's own. A declared popup finds it
        // either way; an externally-created one (a ToolTip's popup, which is NOT in the visual tree) still hosts as long
        // as its target is in a window - the target is always the anchor we position against anyway.
        IUIComponent anchor = EffectiveTarget ?? this;
        _host = anchor.GetVisualAncestors().OfType<IPopupHost>().FirstOrDefault();
        if (_host == null) return;   // not in a window yet; OnAttachedToVisualTree retries
        if (Child is UIComponent child) child.DataContext = DataContext;
        _host.PopupLayer.Add(this);
    }

    private void Close()
    {
        _host?.PopupLayer.Remove(this);
        _host = null;
    }

    // IContainer: the AUML loader sets Child via the [Content] property.
    public void AddOrSetChildComponent(object component) { if (component is IMeasurableComponent c) Child = c; }
    public void RemoveAllChildComponents() => Child = null;
    public IReadOnlyList<object> GetChildComponents() => Child != null ? [Child] : [];
    public void InsertChildComponent(int index, object component) { if (component is IMeasurableComponent c) Child = c; }
    public void RemoveChildComponentAt(int index) => Child = null;
}
