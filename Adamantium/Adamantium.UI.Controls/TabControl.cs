using System;
using System.Collections;
using System.Collections.Specialized;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// A single-select <see cref="Selector"/> whose items are tabs. Each tab's <see cref="TabItem.Header"/> is
/// laid out in the tab strip (the template's ItemsPresenter); the selected tab's body is surfaced as
/// <see cref="SelectedContent"/> and shown by the template's <c>PART_SelectedContentHost</c>. Tabs may be authored
/// directly (<c>&lt;TabControl&gt;&lt;TabItem Header="A"&gt;…&lt;/TabItem&gt;…</c>) or data-bound via
/// <see cref="ItemsControl.ItemsSource"/> (the item becomes the header, and its content the body). The first tab is
/// selected automatically.
/// </summary>
public class TabControl : Selector
{
    public static readonly AdamantiumProperty SelectedContentProperty = AdamantiumProperty.Register(nameof(SelectedContent),
        typeof(object), typeof(TabControl), new PropertyMetadata(null));

    // Body templating for data-bound tabs. When ItemsSource is a collection of view-models, the selected one becomes
    // SelectedContent; the template's PART_SelectedContentHost renders it through these, so each tab VM shows its own
    // View. ContentTemplateSelector picks per VM type (a data-template selector), ContentTemplate is a single template
    // for all. Headers use the inherited ItemTemplate/ItemTemplateSelector (flowed onto each TabItem's HeaderTemplate).
    public static readonly AdamantiumProperty ContentTemplateProperty = AdamantiumProperty.Register(nameof(ContentTemplate),
        typeof(DataTemplate), typeof(TabControl), new PropertyMetadata(null));

    public static readonly AdamantiumProperty ContentTemplateSelectorProperty = AdamantiumProperty.Register(nameof(ContentTemplateSelector),
        typeof(DataTemplateSelector), typeof(TabControl), new PropertyMetadata(null));

    /// <summary>Which edge the tab strip sits on (default <see cref="TabStripPlacement.Top"/>). Each value selects its
    /// own control template via a theme trigger, so a placement can fully restyle the strip.</summary>
    public static readonly AdamantiumProperty TabStripPlacementProperty = AdamantiumProperty.Register(nameof(TabStripPlacement),
        typeof(TabStripPlacement), typeof(TabControl), new PropertyMetadata(TabStripPlacement.Top));

    // Drag-reorder animation feel - theme-settable so the motion is declared, not hard-coded. Duration drives both the
    // neighbour "slide out of the way" and the dragged tab's "settle into its slot" (and slide-home on a short drag);
    // Easing shapes them (null -> a decelerate cubic).
    public static readonly AdamantiumProperty ReorderAnimationDurationProperty = AdamantiumProperty.Register(
        nameof(ReorderAnimationDuration), typeof(TimeSpan), typeof(TabControl),
        new PropertyMetadata(TimeSpan.FromMilliseconds(180)));

    public static readonly AdamantiumProperty ReorderEasingProperty = AdamantiumProperty.Register(
        nameof(ReorderEasing), typeof(IEasingFunction), typeof(TabControl), new PropertyMetadata(null));

    // Content-area chrome (the panel below the tab strip that hosts the selected body). Set by the theme, TemplateBound
    // by the default template - so a theme/accent swap restyles it and a host can retheme it.
    public static readonly AdamantiumProperty BorderBrushProperty = AdamantiumProperty.Register(nameof(BorderBrush),
        typeof(Brush), typeof(TabControl), new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty BorderThicknessProperty = AdamantiumProperty.Register(nameof(BorderThickness),
        typeof(Thickness), typeof(TabControl), new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
        typeof(CornerRadius), typeof(TabControl), new PropertyMetadata(default(CornerRadius), PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty PaddingProperty = AdamantiumProperty.Register(nameof(Padding),
        typeof(Thickness), typeof(TabControl),
        new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    public Brush BorderBrush
    {
        get => GetValue<Brush>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue<CornerRadius>(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public TabControl()
    {
        SelectionChanged += (_, _) => UpdateSelectedContent();
        Items.CollectionChanged += OnItemsChanged;
    }

    /// <summary>The body of the selected tab, shown by the template's <c>PART_SelectedContentHost</c>. Read-only: the
    /// control derives it from the selection.</summary>
    public object SelectedContent
    {
        get => GetValue(SelectedContentProperty);
        private set => SetValue(SelectedContentProperty, value);
    }

    /// <summary>Template for the selected tab's body when items are data (view-models). Rendered by the template's
    /// <c>PART_SelectedContentHost</c>. Use <see cref="ContentTemplateSelector"/> to vary it per item type.</summary>
    public DataTemplate ContentTemplate
    {
        get => GetValue<DataTemplate>(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>Picks the body template per selected item (its view-model type), e.g. one View per tab view-model.</summary>
    public DataTemplateSelector ContentTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(ContentTemplateSelectorProperty);
        set => SetValue(ContentTemplateSelectorProperty, value);
    }

    public TabStripPlacement TabStripPlacement
    {
        get => GetValue<TabStripPlacement>(TabStripPlacementProperty);
        set => SetValue(TabStripPlacementProperty, value);
    }

    /// <summary>How long a drag-reorder slide/settle takes. Theme-settable.</summary>
    public TimeSpan ReorderAnimationDuration
    {
        get => GetValue<TimeSpan>(ReorderAnimationDurationProperty);
        set => SetValue(ReorderAnimationDurationProperty, value);
    }

    /// <summary>Easing for the drag-reorder animations; null uses a decelerate cubic. Theme-settable.</summary>
    public IEasingFunction ReorderEasing
    {
        get => GetValue<IEasingFunction>(ReorderEasingProperty);
        set => SetValue(ReorderEasingProperty, value);
    }

    private void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // Keep a valid tab selected as the collection mutates (WPF selects the first tab by default and never leaves the
        // selection dangling past the end). Writing SelectedIndex drives the base selection machinery.
        if (Items.Count == 0) SelectedIndex = -1;
        else if (SelectedIndex < 0) SelectedIndex = 0;
        else if (SelectedIndex >= Items.Count) SelectedIndex = Items.Count - 1;
        UpdateSelectedContent();
    }

    private void UpdateSelectedContent()
    {
        var item = SelectedItem;
        // An authored TabItem carries its own body in Content; a data item IS the body (shown via the host's ContentTemplate).
        SelectedContent = item is TabItem tab ? tab.Content : item;
    }

    /// <summary>Selects the tab hosted by <paramref name="container"/> (called when its header is clicked).</summary>
    internal void SelectTab(TabItem container)
    {
        var index = ItemContainerGenerator.IndexFromContainer(container);
        if (index >= 0) SelectedIndex = index;
    }

    // --- Drag reorder (animated, visual-first) --------------------------------------------------------------------
    // During a drag the Items collection is NOT reordered - tab layout stays put (stable Bounds), nothing is rebuilt. The
    // dragged tab tracks the cursor via its RenderTransform; the tabs between its start slot and the current target slot
    // slide aside (animated) to open a gap. On release the dragged tab animates into the gap, the reorder is committed
    // ONCE, and the transforms are cleared - so the committed layout matches where everything already sits. If the target
    // is still the start index (the cursor never passed a neighbour's centre) it just slides home. Tabs are content-sized,
    // so a tab makes room by exactly the dragged tab's extent, and the dragged tab's drop shift is the summed extents of
    // the tabs it crossed.

    private TabItem _dragged;
    private bool _dragVertical;
    private double _grabOffset;      // where along the dragged tab the pointer grabbed
    private double _draggedExtent;   // the dragged tab's extent along the strip axis
    private int _dragStartIndex;
    private int _targetIndex;

    private static readonly IEasingFunction DefaultReorderEasing = new CubicEasing { Mode = EasingMode.Out };

    internal void BeginDrag(TabItem tab, MouseEventArgs e)
    {
        if (ItemsHostPanel is not { } panel) return;
        var vertical = TabStripPlacement is TabStripPlacement.Left or TabStripPlacement.Right;
        var pos = e.GetPosition(panel);
        BeginDrag(tab, vertical ? pos.Y : pos.X);
    }

    // Core (position given as the coordinate ALONG the strip axis, in the items-host panel's space) - unit-testable.
    internal void BeginDrag(TabItem tab, double along)
    {
        _dragVertical = TabStripPlacement is TabStripPlacement.Left or TabStripPlacement.Right;
        _dragged = tab;
        _dragStartIndex = _targetIndex = ItemContainerGenerator.IndexFromContainer(tab);
        _grabOffset = along - SlotStart(tab);
        _draggedExtent = Extent(tab);
        tab.ZIndex = 1;   // float above its siblings for the drag
        SetOffset(tab, 0);
    }

    /// <summary>Each mouse-move while dragging: keep the tab under the cursor and slide the passed tabs aside to open the
    /// gap at the current target index.</summary>
    internal void UpdateDrag(TabItem tab, MouseEventArgs e)
    {
        if (ItemsHostPanel is not { } panel) return;
        var pos = e.GetPosition(panel);
        UpdateDrag(tab, _dragVertical ? pos.Y : pos.X);
    }

    internal void UpdateDrag(TabItem tab, double along)
    {
        if (!ReferenceEquals(tab, _dragged)) return;

        SetOffset(tab, along - _grabOffset - SlotStart(tab));   // Bounds are stable during the drag -> exact follow

        // Target index = how far the dragged tab's centre has passed the OTHER tabs' (stable) centres.
        var centre = along - _grabOffset + _draggedExtent / 2;
        var target = _dragStartIndex;
        for (var i = 0; i < Items.Count; i++)
        {
            if (i == _dragStartIndex || ItemContainerGenerator.ContainerFromIndex(i) is not TabItem other) continue;
            var otherCentre = SlotStart(other) + Extent(other) / 2;
            if (i > _dragStartIndex && centre > otherCentre) target = Math.Max(target, i);
            else if (i < _dragStartIndex && centre < otherCentre) target = Math.Min(target, i);
        }

        if (target != _targetIndex)
        {
            _targetIndex = target;
            ApplyGapOffsets();
        }
    }

    internal void EndDrag(TabItem tab)
    {
        if (!ReferenceEquals(tab, _dragged)) return;
        _dragged = null;
        var start = _dragStartIndex;
        var target = _targetIndex;
        // Slide into the gap (target == start => slide home), then commit the reorder once and clear the transforms so
        // the committed layout matches where everything already is. Drop the raised z-order after it settles.
        AnimateOffsetTo(tab, GapShift(start, target), () =>
        {
            MoveItem(start, target);
            ClearDragOffsets();
            tab.ZIndex = 0;
        });
    }

    // Each other tab slides aside by the dragged tab's extent while it sits between the start and target slots (opening
    // the gap the dragged tab will drop into); everything else returns to 0.
    private void ApplyGapOffsets()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (i == _dragStartIndex || ItemContainerGenerator.ContainerFromIndex(i) is not TabItem other) continue;

            double gap = 0;
            if (_targetIndex > _dragStartIndex && i > _dragStartIndex && i <= _targetIndex) gap = -_draggedExtent;
            else if (_targetIndex < _dragStartIndex && i >= _targetIndex && i < _dragStartIndex) gap = +_draggedExtent;
            AnimateOffsetTo(other, gap);
        }
    }

    // The dragged tab's net shift from its start slot to the target slot = the summed extents of the tabs it crosses.
    private double GapShift(int start, int target)
    {
        double shift = 0;
        if (target > start)
            for (var i = start + 1; i <= target; i++)
            {
                if (ItemContainerGenerator.ContainerFromIndex(i) is TabItem t) shift += Extent(t);
            }
        else if (target < start)
            for (var i = target; i < start; i++)
            {
                if (ItemContainerGenerator.ContainerFromIndex(i) is TabItem t) shift -= Extent(t);
            }
        return shift;
    }

    private void ClearDragOffsets()
    {
        for (var i = 0; i < Items.Count; i++)
            if (ItemContainerGenerator.ContainerFromIndex(i) is TabItem t && t.RenderTransform is Transform)
                SetOffset(t, 0);
    }

    private double SlotStart(TabItem tab) => _dragVertical ? tab.Bounds.Y : tab.Bounds.X;

    private double Extent(TabItem tab) => _dragVertical ? tab.Bounds.Height : tab.Bounds.Width;

    // Direct offset write (no animation): cancel any running slide first, else the Animation-priority value masks it.
    private void SetOffset(TabItem tab, double offset)
    {
        var transform = EnsureTransform(tab);
        var prop = _dragVertical ? Transform.TranslateYProperty : Transform.TranslateXProperty;
        transform.CancelAnimation(prop);
        if (_dragVertical) transform.TranslateY = offset; else transform.TranslateX = offset;
    }

    private double CurrentOffset(TabItem tab) =>
        tab.RenderTransform is not Transform t ? 0 : (_dragVertical ? t.TranslateY : t.TranslateX);

    // Animate the offset from its current value to `to`.
    private void AnimateOffsetTo(TabItem tab, double to, Action completed = null)
    {
        var transform = EnsureTransform(tab);
        var prop = _dragVertical ? Transform.TranslateYProperty : Transform.TranslateXProperty;
        transform.BeginAnimation(prop, new DoubleAnimation
        {
            From = CurrentOffset(tab), To = to, Duration = ReorderAnimationDuration, Easing = ReorderEasing ?? DefaultReorderEasing
        }, completed);
    }

    private static Transform EnsureTransform(TabItem tab)
    {
        if (tab.RenderTransform is Transform t) return t;
        var transform = new Transform();
        tab.RenderTransform = transform;
        return transform;
    }

    // Reorder in place, preserving which item is selected (its index shifts). Mutates ItemsSource when data-bound to a
    // writable list, otherwise the authored Items collection.
    private void MoveItem(int from, int to)
    {
        if (from == to) return;
        var selected = SelectedItem;

        if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } src && to < src.Count)
        {
            var item = src[from];
            src.RemoveAt(from);
            src.Insert(to, item);
        }
        else
        {
            var item = Items[from];
            Items.RemoveAt(from);
            Items.Insert(to, item);
        }

        if (selected != null) SelectedItem = selected;   // re-point SelectedIndex at the moved item's new slot
    }

    /// <summary>Whether <paramref name="container"/> is the selected tab - by the item it hosts, so it holds for both an
    /// authored TabItem (it IS the selected item) and a generated one (its DataContext is).</summary>
    internal bool IsContainerSelected(TabItem container) =>
        SelectedItem != null &&
        (ReferenceEquals(SelectedItem, container) || ReferenceEquals(SelectedItem, container.DataContext));

    // --- Container seam: TabControl hosts items in TabItem containers ---------------------------------------------

    protected internal override bool IsItemItsOwnContainer(object item) => item is TabItem;

    protected internal override IUIComponent GetContainerForItem()
    {
        var container = new TabItem();
        if (ItemContainerStyle != null) container.AttachStyles(ItemContainerStyle);
        return container;
    }

    protected internal override void PrepareContainer(IUIComponent container, object item)
    {
        // Only for GENERATED containers (data items). An authored TabItem is its own container and is never prepared -
        // it already carries its Header/Content from markup.
        if (container is TabItem tab && !ReferenceEquals(tab, item))
        {
            tab.DataContext = item;
            tab.Header = item;
            tab.Content = item;
            // A TabControl's ItemTemplate is the HEADER template (WPF semantics): flow it onto the tab so the strip label
            // renders the item via a template, while the body uses ContentTemplate/ContentTemplateSelector.
            tab.HeaderTemplate = ItemTemplate;
            tab.HeaderTemplateSelector = ItemTemplateSelector;
            ApplyContainerSelection(tab, item);
        }
    }

    protected internal override void ClearContainer(IUIComponent container)
    {
        if (container is TabItem tab)
        {
            tab.DataContext = null;
            tab.IsSelected = false;
        }
    }
}
