using Adamantium.UI.Core;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Base;

public class MeasurableUIComponent : ObservableUIComponent, IName, IMeasurableComponent
{
    private Size? _previousMeasure;
    private Rect? _previousArrange;
    

    static MeasurableUIComponent()
    {
        //SizeChangedEvent.RegisterClassHandler<UIComponent>(new SizeChangedEventHandler(SizeChangedHandler));
    }
        
    public MeasurableUIComponent()
    { }
    
    public static readonly AdamantiumProperty TagProperty = AdamantiumProperty.Register(nameof(Tag),
        typeof(object), typeof(MeasurableUIComponent), new PropertyMetadata(null));

    public static readonly AdamantiumProperty WidthProperty = AdamantiumProperty.Register(nameof(Width),
        typeof(Double), typeof(MeasurableUIComponent),
        new PropertyMetadata(Double.NaN, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender, WidthChangedCallBack));

    public static readonly AdamantiumProperty HeightProperty = AdamantiumProperty.Register(nameof(Height),
        typeof(Double), typeof(MeasurableUIComponent),
        new PropertyMetadata(Double.NaN, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender, HeightChangedCallBack));

    public static readonly AdamantiumProperty MinWidthProperty = AdamantiumProperty.Register(nameof(MinWidth),
        typeof(Double), typeof(MeasurableUIComponent),
        new PropertyMetadata((Double)0, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty MinHeightProperty = AdamantiumProperty.Register(nameof(MinHeight),
        typeof(Double), typeof(MeasurableUIComponent),
        new PropertyMetadata((Double)0, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty ActualWidthProperty = AdamantiumProperty.RegisterReadOnly(nameof(ActualWidth),
        typeof(Double), typeof(MeasurableUIComponent),
        new PropertyMetadata((Double)0));

    public static readonly AdamantiumProperty ActualHeightProperty = AdamantiumProperty.RegisterReadOnly(nameof(ActualHeight),
        typeof(Double), typeof(MeasurableUIComponent),
        new PropertyMetadata((Double)0));

    public static readonly AdamantiumProperty MaxWidthProperty = AdamantiumProperty.Register(nameof(MaxWidth),
        typeof(Double), typeof(MeasurableUIComponent),
        new PropertyMetadata(Double.PositiveInfinity, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty MaxHeightProperty = AdamantiumProperty.Register(nameof(MaxHeight),
        typeof(Double), typeof(MeasurableUIComponent),
        new PropertyMetadata(Double.PositiveInfinity, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty HorizontalAlignmentProperty = AdamantiumProperty.Register(nameof(HorizontalAlignment),
        typeof(HorizontalAlignment), typeof(MeasurableUIComponent), new PropertyMetadata(HorizontalAlignment.Stretch, PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty VerticalAlignmentProperty = AdamantiumProperty.Register(nameof(VerticalAlignment),
        typeof(VerticalAlignment), typeof(MeasurableUIComponent), new PropertyMetadata(VerticalAlignment.Stretch, PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty MarginProperty = AdamantiumProperty.Register(nameof(Margin),
        typeof(Thickness), typeof(MeasurableUIComponent), new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    // CSS-style single-side margins. Each is an OPTIONAL override of ONE side of Margin: NaN (the default) means "not set,
    // defer to Margin's side"; a real value overrides just that side. Nothing writes back into Margin - the effective
    // margin is composed at layout time (see EffectiveMargin), so each side is independently settable/bindable/animatable
    // and never fights Margin's own value or priority.
    public static readonly AdamantiumProperty MarginLeftProperty = AdamantiumProperty.Register(nameof(MarginLeft),
        typeof(Double), typeof(MeasurableUIComponent),
        new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange, OnMarginSideChanged));

    public static readonly AdamantiumProperty MarginTopProperty = AdamantiumProperty.Register(nameof(MarginTop),
        typeof(Double), typeof(MeasurableUIComponent),
        new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange, OnMarginSideChanged));

    public static readonly AdamantiumProperty MarginRightProperty = AdamantiumProperty.Register(nameof(MarginRight),
        typeof(Double), typeof(MeasurableUIComponent),
        new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange, OnMarginSideChanged));

    public static readonly AdamantiumProperty MarginBottomProperty = AdamantiumProperty.Register(nameof(MarginBottom),
        typeof(Double), typeof(MeasurableUIComponent),
        new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange, OnMarginSideChanged));

    public static readonly AdamantiumProperty UseLayoutRoundingProperty = AdamantiumProperty.Register(nameof(UseLayoutRounding),
        typeof(Boolean), typeof(MeasurableUIComponent), new PropertyMetadata(false, PropertyMetadataOptions.AffectsArrange));
    
    public static readonly RoutedEvent SizeChangedEvent = 
        EventManager.RegisterRoutedEvent(nameof(SizeChanged),
            RoutingStrategy.Bubble, typeof(SizeChangedEventHandler), typeof(UIComponent));
    
    public event SizeChangedEventHandler SizeChanged
    {
        add => AddHandler(SizeChangedEvent, value);
        remove => RemoveHandler(SizeChangedEvent, value);
    }

    /// <summary>A real length - not an auto (NaN) one and not the Unset sentinel a cleared style/trigger leaves.</summary>
    private static bool IsConcrete(object value, out double length)
    {
        length = 0;
        if (value is not double d || double.IsNaN(d)) return false;

        length = d;
        return true;
    }

    private static void WidthChangedCallBack(AdamantiumComponent adamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        if (adamantiumComponent is not MeasurableUIComponent o) return;
        Size old = default;
        // Only a transition between two CONCRETE widths is a size change; layout re-measures on its own anyway.
        if (!IsConcrete(e.OldValue, out var oldWidth) || !IsConcrete(e.NewValue, out var newWidth))
            return;

        old.Width = oldWidth;
        old.Height = o.Height;

        var newSize = new Size(newWidth, o.Height);
        var args = new SizeChangedEventArgs(old, newSize, true, false);
        args.RoutedEvent = SizeChangedEvent;
        o.OnSizeChanged(args);
        o.RaiseEvent(args);
    }
        
    private static void HeightChangedCallBack(AdamantiumComponent adamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(adamantiumComponent is MeasurableUIComponent o)) return;
        // See WidthChangedCallBack.
        if (!IsConcrete(e.OldValue, out var oldHeight) || !IsConcrete(e.NewValue, out var newHeight))
            return;

        var old = new Size(o.Width, oldHeight);
        var newSize = new Size(o.Width, newHeight);
        var args = new SizeChangedEventArgs(old, newSize, false, true);
        args.RoutedEvent = SizeChangedEvent;
        o.OnSizeChanged(args);
        o?.RaiseEvent(args);
    }
    
    protected virtual void OnSizeChanged(SizeChangedEventArgs e)
    {
            
    }

    public Double Width
    {
        get => GetValue<Double>(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    public Double Height
    {
        get => GetValue<Double>(HeightProperty);
        set => SetValue(HeightProperty, value);
    }

    public Double ActualWidth
    {
        get => GetValue<Double>(ActualWidthProperty);
        private set => SetValue(ActualWidthProperty, value);
    }

    public Double ActualHeight
    {
        get => GetValue<Double>(ActualHeightProperty);
        private set => SetValue(ActualHeightProperty, value);
    }

    public Double MinWidth
    {
        get => GetValue<Double>(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    public Double MinHeight
    {
        get => GetValue<Double>(MinHeightProperty);
        set => SetValue(MinHeightProperty, value);
    }

    public Double MaxWidth
    {
        get => GetValue<Double>(MaxWidthProperty);
        set => SetValue(MaxWidthProperty, value);
    }

    public Double MaxHeight
    {
        get => GetValue<Double>(MaxHeightProperty);
        set => SetValue(MaxHeightProperty, value);
    }

    public Thickness Margin
    {
        get => GetValue<Thickness>(MarginProperty);
        set => SetValue(MarginProperty, value);
    }

    /// <summary>An optional override of just <see cref="Thickness.Left"/> of <see cref="Margin"/>, so a one-sided offset is
    /// <c>MarginLeft="8"</c> instead of <c>Margin="8,0,0,0"</c>. Unset by default (the getter returns Margin.Left); once
    /// assigned it overrides that side only. The effective margin is composed at layout, so Margin and the per-side
    /// overrides never fight over value or priority - each is independently settable, bindable and animatable.</summary>
    public Double MarginLeft
    {
        get { var v = GetValue<Double>(MarginLeftProperty); return Double.IsNaN(v) ? Margin.Left : v; }
        set => SetValue(MarginLeftProperty, value);
    }

    /// <summary>An optional override of just <see cref="Thickness.Top"/> of <see cref="Margin"/> (see <see cref="MarginLeft"/>).</summary>
    public Double MarginTop
    {
        get { var v = GetValue<Double>(MarginTopProperty); return Double.IsNaN(v) ? Margin.Top : v; }
        set => SetValue(MarginTopProperty, value);
    }

    /// <summary>An optional override of just <see cref="Thickness.Right"/> of <see cref="Margin"/> (see <see cref="MarginLeft"/>).</summary>
    public Double MarginRight
    {
        get { var v = GetValue<Double>(MarginRightProperty); return Double.IsNaN(v) ? Margin.Right : v; }
        set => SetValue(MarginRightProperty, value);
    }

    /// <summary>An optional override of just <see cref="Thickness.Bottom"/> of <see cref="Margin"/> (see <see cref="MarginLeft"/>).</summary>
    public Double MarginBottom
    {
        get { var v = GetValue<Double>(MarginBottomProperty); return Double.IsNaN(v) ? Margin.Bottom : v; }
        set => SetValue(MarginBottomProperty, value);
    }

    // Set once ANY per-side override is assigned, so layout skips the compose for the common case (no overrides).
    private bool _hasSideMargin;

    private static void OnMarginSideChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var c = (MeasurableUIComponent)a;
        c._hasSideMargin = !Double.IsNaN(c.GetValue<Double>(MarginLeftProperty))
                        || !Double.IsNaN(c.GetValue<Double>(MarginTopProperty))
                        || !Double.IsNaN(c.GetValue<Double>(MarginRightProperty))
                        || !Double.IsNaN(c.GetValue<Double>(MarginBottomProperty));
    }

    /// <summary>The margin layout actually uses: <see cref="Margin"/> with any per-side override applied on top. Composed
    /// only when an override exists, so a control without per-side margins pays a single flag read.</summary>
    public Thickness EffectiveMargin
    {
        get
        {
            var m = Margin;
            if (!_hasSideMargin) return m;
            var l = GetValue<Double>(MarginLeftProperty);
            var t = GetValue<Double>(MarginTopProperty);
            var r = GetValue<Double>(MarginRightProperty);
            var b = GetValue<Double>(MarginBottomProperty);
            return new Thickness(
                Double.IsNaN(l) ? m.Left : l,
                Double.IsNaN(t) ? m.Top : t,
                Double.IsNaN(r) ? m.Right : r,
                Double.IsNaN(b) ? m.Bottom : b);
        }
    }

    public VerticalAlignment VerticalAlignment
    {
        get => GetValue<VerticalAlignment>(VerticalAlignmentProperty);
        set => SetValue(VerticalAlignmentProperty, value);
    }

    public HorizontalAlignment HorizontalAlignment
    {
        get => GetValue<HorizontalAlignment>(HorizontalAlignmentProperty);
        set => SetValue(HorizontalAlignmentProperty, value);
    }

    public object Tag
    {
        get => GetValue(TagProperty);
        set => SetValue(TagProperty, value);
    }
    
    public bool UseLayoutRounding
    {
        get => GetValue<bool>(UseLayoutRoundingProperty);
        set => SetValue(UseLayoutRoundingProperty, value);
    }
    
    public bool IsMeasureValid { get; private set; }

    // True while THIS element's MeasureCore runs - i.e. while it is measuring its own children. A child whose desired
    // size changes during that cascade must not invalidate us: we are computing our size from it right now.
    private bool _measuring;

    public bool IsArrangeValid { get; private set; }

    /// <summary>Zero while COLLAPSED - it asks its parent for no space, which is what collapsing means, and matches what
    /// <see cref="UIComponent.RenderSize"/> already reports. Keeping the last measured size instead let a hidden element
    /// go on occupying its parent's layout, and let a SKIPPED measure hand out a size from a constraint that no longer
    /// applies (a hidden tab's stale width raised a scrollbar on the visible one).</summary>
    public Size DesiredSize
    {
        get => Visibility == Visibility.Collapsed ? Size.Zero : _desiredSize;
        private set => _desiredSize = value;
    }

    private Size _desiredSize;

    /// <summary>The rect this element was last arranged with (its last correct slot), preserved across invalidation so
    /// the layout manager can re-arrange the element into it. Null until the first arrange.</summary>
    public Rect? PreviousArrangeSlot => _previousArrange;

    /// <summary>The available size this element was last measured with (its cached constraint), preserved across
    /// invalidation so the layout manager can re-measure the element with it. Null until the first measure.</summary>
    public Size? PreviousMeasureConstraint => _previousMeasure;

    // Test/diagnostics hook: process-wide count of Measure()/Arrange() invocations across all instances. A perf test
    // asserts a clean frame (nothing invalidated) triggers zero of these - i.e. the dirty-queue layout manager touches
    // nothing when nothing is dirty (vs. the old full-tree walk, which called Measure/Arrange on every node each frame).
    public static long TotalMeasureCalls { get; private set; }

    // Test hook: measures that actually ran MeasureCore (real work), vs TotalMeasureCalls which also counts short-circuits.
    public static long TotalMeasureCores { get; private set; }

    public static long TotalArrangeCalls { get; private set; }

    // Test hook: arranges that actually did WORK (ran ArrangeCore + recursed), vs TotalArrangeCalls which also counts
    // arranges that short-circuited on an unchanged rect. Transform-only scroll keeps a staying tile's rect constant, so
    // it short-circuits here; a steady-state scroll runs ArrangeCore only for the rows that entered the window.
    public static long TotalArrangeCores { get; private set; }

    /// <summary>
    /// Measures the control and its child elements as part of a layout pass.
    /// </summary>
    /// <param name="availableSize">The size available to the control.</param>
    /// <returns>The desired size for the control.</returns>
    protected virtual Size MeasureOverride(Size availableSize)
    {
        double width = 0;
        double height = 0;

        foreach (var visual in VisualChildren)
        {
            var child = (MeasurableUIComponent)visual;
            child.Measure(availableSize);
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }

        // Layout is a VISUAL-tree walk ONLY (above) - it is the complete layout tree; the logical tree is for inheritance/
        // resources/DataContext/events, never layout (same as WPF). A control that wants a child laid out MUST add it to
        // its visual children. Do NOT re-add a LogicalChildren loop: it would measure a templated control's Content twice
        // (once via its ContentPresenter, once here) - the 2x pass over every templated element's subtree. Portalled
        // children (a Popup's Child) are sized by their overlay host (PopupLayer), not here.

        if (UseLayoutRounding)
        {
            width = Math.Ceiling(width);
            height = Math.Ceiling(height);
        }

        return new Size(width, height);
    }

    /// <summary>
    /// Carries out a measure of the control.
    /// </summary>
    /// <param name="availableSize">The available size for the control.</param>
    /// <param name="force">
    /// If true, the control will be measured even if <paramref name="availableSize"/> has not
    /// changed from the last measure.
    /// </param>
    public void Measure(Size availableSize, bool force = false)
    {
        TotalMeasureCalls++;
        if (Core.Diagnostics.LayoutTrace.Counting) Core.Diagnostics.LayoutTrace.Count(GetType(), "*measure-call*");
        if (Double.IsNaN(availableSize.Width) || Double.IsNaN(availableSize.Height))
        {
            throw new InvalidOperationException("Cannot call Measure using a size with NaN values.");
        }

        if (force || !IsMeasureValid || _previousMeasure != availableSize)
        {
            TotalMeasureCores++;
            IsMeasureValid = true;
            IsArrangeValid = false;
            InvalidateGeometryFromLayout();

            var previousDesired = DesiredSize;

            _measuring = true;
            var measureFrame = Core.Diagnostics.RuntimeStats.BeginLayoutFrame();
            var desiredSize = MeasureCore(availableSize).Constrain(availableSize);
            Core.Diagnostics.RuntimeStats.EndLayoutFrame(GetType(), measureFrame);
            _measuring = false;

            if (IsInvalidSize(desiredSize))
            {
                throw new InvalidOperationException("Invalid size returned for Measure.");
            }

            DesiredSize = desiredSize;

            // A CHANGED desired size is news for the parent, whoever asked for this measure. The layout pass propagates
            // this too, but only for a node it dequeued itself - and a node measured any other way (a parent's cascade,
            // a direct Measure, an inline re-measure from arrange) then goes valid holding a size nobody above it has
            // seen. The pass afterwards skips it on the validity gate, silently, and every ancestor keeps the number it
            // had. Measured on a docking panel returning from a window: its tab strip measured 272x36 while the grid one
            // level up stayed at 0 and "valid", so the strip was drawn empty until something else disturbed the tree.
            // NOT while the parent is measuring US - that cascade is already computing its own size from this one.
            if (previousDesired != desiredSize)
            {
                NotifyParentOfContributionChange();
            }
            // Cache the AVAILABLE size this measure ran with - the gate above compares the next availableSize against
            // it to skip a redundant re-measure. (Storing DesiredSize here was the bug: desired != available for any
            // control that doesn't fill its slot, so the gate always missed and EVERY such control - e.g. a Path with
            // fixed bounds in a star cell - re-measured + re-tessellated on every parent measure, tanking animation.)
            _previousMeasure = availableSize;
            if (LayoutTrace.Enabled) LayoutTrace.Log($"  MEASURE {LayoutName}: avail={availableSize} -> desired={DesiredSize}");
        }
        else if (LayoutTrace.Enabled)
        {
            LayoutTrace.Log($"  MEASURE {LayoutName}: SKIP avail={availableSize} desired={DesiredSize}");
        }
    }

    /// <summary>Name (or type) for layout trace messages.</summary>
    private string LayoutName => string.IsNullOrEmpty(Name) ? GetType().Name : Name;

    /// <summary>
    /// Positions child elements as part of a layout pass.
    /// </summary>
    /// <param name="finalSize">The size available to the control.</param>
    /// <returns>The actual size used.</returns>
    protected virtual Size ArrangeOverride(Size finalSize)
    {
        foreach (var visual in VisualChildren)
        {
            var child = (IMeasurableComponent)visual;
            child.Arrange(new Rect(finalSize));
        }

        // Arrange the VISUAL tree ONLY (above) - see MeasureOverride. No LogicalChildren loop.

        return finalSize;
    }

    /// <summary>
    /// Arranges the control and its children.
    /// </summary>
    /// <param name="rect">The control's new bounds.</param>
    /// <param name="force">
    /// If true, the control will be arranged even if <paramref name="rect"/> has not changed
    /// from the last arrange.
    /// </param>
    public void Arrange(Rect rect, bool force = false)
    {
        TotalArrangeCalls++;
        if (Core.Diagnostics.LayoutTrace.Counting)
        {
            Core.Diagnostics.LayoutTrace.Count(GetType(), "*arrange-call*");
            Core.Diagnostics.LayoutTrace.CountCaller(GetType(), "arrange", 1);
        }
        if (IsInvalidRect(rect))
        {
            throw new InvalidOperationException("Invalid Arrange rectangle.");
        }

        // Measure was invalidated after this arrange was scheduled (classically: a virtualized container's content is
        // rebound mid-pass, invalidating the inner ContentPresenter's measure). Arrange needs a valid measure. ABORTING
        // here was wrong: the node then gets re-arranged later by the manager into its OWN cached slot, while its parent -
        // which arranged to a NEW size and is now arrange-valid - never re-cascades into it, freezing the node at a
        // PREVIOUS size (the recycled tiles stuck at an old cell size). Instead re-measure inline with the cached
        // constraint and fall through to arrange into the slot the parent is giving NOW. A node that was never measured
        // (no cached constraint) genuinely can't be arranged yet -> keep aborting.
        if (!IsMeasureValid)
        {
            if (_previousMeasure is not { } cachedConstraint)
            {
                if (LayoutTrace.Enabled) LayoutTrace.Log($"  ARRANGE {LayoutName}: ABORT (never measured) rect={rect}");
                return;
            }
            Measure(cachedConstraint);
        }

        if (force || !IsArrangeValid || _previousArrange != rect)
        {
            IsArrangeValid = true;
            TotalArrangeCores++;
            var arrangeFrame = Core.Diagnostics.RuntimeStats.BeginLayoutFrame();
            ArrangeCore(rect);
            Core.Diagnostics.RuntimeStats.EndLayoutFrame(GetType(), arrangeFrame);
            _previousArrange = rect;
            if (LayoutTrace.Enabled) LayoutTrace.Log($"  ARRANGE {LayoutName}: rect={rect} -> bounds={Bounds} render={RenderSize}");
        }
        else if (LayoutTrace.Enabled)
        {
            LayoutTrace.Log($"  ARRANGE {LayoutName}: SKIP rect={rect} bounds={Bounds}");
        }
        
        // if (LogicalParent != null)
        // {
        //     Location = Bounds.Location + ((IUIComponent)LogicalParent).Location;
        //     ClipPosition = ClipRectangle.Location + ((IUIComponent)LogicalParent).Location;
        // }
        // else
        // {
        //     Location = Bounds.Location;
        //     ClipPosition = ClipRectangle.Location;
        // }
    }

    /// <summary>
    /// The default implementation of the control's measure pass.
    /// </summary>
    /// <param name="availableSize">The size available to the control.</param>
    /// <returns>The desired size for the control.</returns>
    /// <remarks>
    /// This method calls <see cref="MeasureOverride(Size)"/> which is probably the method you
    /// want to override in order to modify a control's arrangement.
    /// </remarks>
    protected Size MeasureCore(Size availableSize)
    {
        if (Visibility is Visibility.Visible or Visibility.Hidden)
        {
            var margin = EffectiveMargin;

            Size constrained;
            Matrix4x4 lt = default;
            var hasLayout = false;

            // IWindow is top level control. Constraints should be ignored by top level controls
            // because it will lead to incorrect measurements
            if (this is IWindow)
            {
                constrained = availableSize;
                margin = new Thickness(0);
            }
            else
            {
                // With a LayoutTransform, measure the content in its OWN (untransformed) space: turn the parent-space
                // available size into the local size whose transform fits it, then constrain + measure there.
                var available = availableSize.Deflate(margin);
                hasLayout = TryGetLayoutMatrix(out lt);
                if (hasLayout) available = InverseTransformSize(lt, available);
                constrained = this.ApplyLayoutConstraints(available);
            }

            var measured = MeasureOverride(constrained);
            var width = measured.Width;
            var height = measured.Height;

            if (!Double.IsNaN(Width))
            {
                width = Math.Max(width, Width);
            }

            width = Math.Min(width, MaxWidth);
            width = Math.Max(width, MinWidth);

            if (!Double.IsNaN(Height))
            {
                height = Math.Max(height, Height);
            }

            height = Math.Min(height, MaxHeight);
            height = Math.Max(height, MinHeight);

            // Report the transform's BOUNDING BOX to the parent (the footprint the scaled/rotated content occupies).
            var desired = new Size(width, height);
            if (hasLayout) desired = TransformSize(lt, desired);
            return NonNegative(desired.Inflate(margin));
        }
        else
        {
            return new Size();
        }
    }

    /// <summary>
    /// The default implementation of the control's arrange pass.
    /// </summary>
    /// <param name="finalRect">The control's new bounds.</param>
    /// <remarks>
    /// This method calls <see cref="ArrangeOverride(Size)"/> which is probably the method you
    /// want to override in order to modify a control's arrangement.
    /// </remarks>
    protected void ArrangeCore(Rect finalRect)
    {
        if (Visibility is Visibility.Visible or Visibility.Hidden)
        {
            var margin = EffectiveMargin;

            // IWindow is top level control. Margin should be ignored by top level controls
            // because there is no element to margin from for IWindow
            if (this is IWindow)
            {
                margin = new Thickness(0);
            }
                
            double originX = finalRect.X + margin.Left;
            double originY = finalRect.Y + margin.Top;

            var sizeMinusMargins = new Size(
                Math.Max(0, finalRect.Width - margin.Left - margin.Right),
                Math.Max(0, finalRect.Height - margin.Top - margin.Bottom));
            var size = sizeMinusMargins;

            double clipOriginX = originX;
            double clipOriginY = originY;

            // A non-Stretch element does NOT fill the slot its parent gave it: it shrinks to its own desired (content)
            // size. WPF clamps the arrange size to the unclipped DesiredSize (minus margins) here. Clamping to
            // finalRect (as before) was a no-op - size is already finalRect minus margins - so a Left/Top/etc.-aligned
            // element with no explicit size (Width/Height = Auto/NaN) wrongly stretched to the whole parent.
            if (HorizontalAlignment != HorizontalAlignment.Stretch)
            {
                size.Width = Math.Min(size.Width, Math.Max(0, DesiredSize.Width - margin.Left - margin.Right));
            }

            if (VerticalAlignment != VerticalAlignment.Stretch)
            {
                size.Height = Math.Min(size.Height, Math.Max(0, DesiredSize.Height - margin.Top - margin.Bottom));
            }

            // Width/Height/Min/Max constrain the element's OWN size. With a LayoutTransform that own size is the INNER one,
            // so the constraints are applied to the inner arrange size below (after the inverse-transform), not to the outer
            // footprint - clamping the footprint to Width here would cancel the scale.
            var hasLayout = TryGetLayoutMatrix(out var lt);
            if (!hasLayout) size = this.ApplyLayoutConstraints(size);

            if (this is IRootVisualComponent)
            {
                size = DesiredSize;
            }

            if (UseLayoutRounding)
            {
                size = new Size(
                    Math.Ceiling(size.Width),
                    Math.Ceiling(size.Height));
                sizeMinusMargins = new Size(
                    Math.Ceiling(sizeMinusMargins.Width),
                    Math.Ceiling(sizeMinusMargins.Height));
            }

            // With a LayoutTransform, arrange the content in its OWN space (the inverse of the footprint it is given, with
            // the constraints applied there), and the footprint it then occupies is the transform's bounding box. RenderSize
            // is the content (inner) size that LocalTransform scales/rotates; the FOOTPRINT (Bounds/clip/alignment) is outer.
            var innerArrange = hasLayout ? this.ApplyLayoutConstraints(InverseTransformSize(lt, size)) : size;
            var innerUsed = ArrangeOverride(innerArrange).Constrain(innerArrange);
            var outerUsed = hasLayout ? TransformSize(lt, innerUsed) : innerUsed;

            // A size change must re-run OnRender: a control that first rendered at a STALE size (e.g. 0x0 while still
            // unarranged - which happens for content built during a measure pass, like a tab body added via a
            // ContentPresenter/DataTemplate) cached that geometry and, being "geometry-valid", would never redraw at the
            // new size - its fill rect stays 0x0 = invisible. Invalidate the render geometry so the render pass re-records
            // it at the arranged size. Measure already does this (MeasureCore); arrange must too when the size changes.
            var renderSizeChanged = !MathHelper.NearEqual(RenderSize.Width, innerUsed.Width)
                                    || !MathHelper.NearEqual(RenderSize.Height, innerUsed.Height);

            ActualWidth = innerUsed.Width;     // the element's OWN size (= RenderSize): controls draw their geometry at this,
            ActualHeight = innerUsed.Height;   // and LocalTransform then scales it up to the outer footprint (no double-scale)
            RenderSize = innerUsed;
            size = outerUsed;   // the rest of arrange (alignment, clip, bounds) works in the FOOTPRINT (outer) space

            if (renderSizeChanged) InvalidateGeometryFromLayout();

            switch (HorizontalAlignment)
            {
                // Stretch anchors at the START, like Left. When the element fills the slot the offset is 0 either way;
                // when it returns LESS than the slot (a control that can't stretch - e.g. a CheckBox's content stack)
                // it must stay at the slot origin, not drift to the middle. Grouping Stretch with Center slid such
                // content to the centre of its parent the moment the element stopped filling.
                case HorizontalAlignment.Left:
                case HorizontalAlignment.Stretch:
                    size.Width = Math.Min(sizeMinusMargins.Width, outerUsed.Width);
                    break;
                case HorizontalAlignment.Center:
                    originX += (sizeMinusMargins.Width - size.Width) / 2;
                    clipOriginX = Math.Max(originX, finalRect.X + margin.Left);
                    size.Width = Math.Min(sizeMinusMargins.Width, outerUsed.Width);
                    break;
                case HorizontalAlignment.Right:
                    originX += sizeMinusMargins.Width - size.Width;
                    clipOriginX = Math.Max(originX, margin.Left);
                    size.Width = Math.Min(sizeMinusMargins.Width, outerUsed.Width);
                    break;
            }

            switch (VerticalAlignment)
            {
                case VerticalAlignment.Top:
                case VerticalAlignment.Stretch:
                    size.Height = Math.Min(sizeMinusMargins.Height, outerUsed.Height);
                    break;
                case VerticalAlignment.Center:
                    originY += (sizeMinusMargins.Height - size.Height) / 2;
                    clipOriginY = Math.Max(originY, finalRect.Y + margin.Top);
                    size.Height = Math.Min(sizeMinusMargins.Height, outerUsed.Height);
                    break;
                case VerticalAlignment.Bottom:
                    originY += sizeMinusMargins.Height - size.Height;
                    clipOriginY = Math.Max(originY, finalRect.Y);
                    size.Height = Math.Min(sizeMinusMargins.Height, outerUsed.Height);
                    break;
            }

            if (UseLayoutRounding)
            {
                originX = Math.Floor(originX);
                originY = Math.Floor(originY);
            }

            ClipRectangle = new Rect(clipOriginX, clipOriginY,
                size.Width, size.Height);

            var newBounds = new Rect(originX, originY, outerUsed.Width, outerUsed.Height);
            if (Bounds != newBounds)
            {
                Bounds = newBounds;
            }

            if (sizeChanged)
            {
                bool widthChanged = false;
                bool heightChanged = false;
                if (!MathHelper.NearEqual(RenderSize.Width, previousRenderSize.Width))
                {
                    widthChanged = true;
                }
                if (!MathHelper.NearEqual(RenderSize.Height, previousRenderSize.Height))
                {
                    heightChanged = true;
                }
                if (widthChanged || heightChanged)
                {
                    var from = previousRenderSize;
                    // The bookkeeping happens whether or not anyone listens - it is what the NEXT event will report as its
                    // "from", and letting it drift while nobody was subscribed would hand the first late subscriber a size
                    // the element had long ago.
                    previousRenderSize = Bounds.Size;

                    // Only now, and only if the route can hear it, are the args built. RaiseEvent would reach the same
                    // verdict a moment later - but by then the object exists, and at 4K one of these per element per step
                    // for nobody was a third of the drag.
                    if (WouldBeHeard(SizeChangedEvent))
                    {
                        var args = new SizeChangedEventArgs(from, Bounds.Size, widthChanged, heightChanged)
                        {
                            RoutedEvent = SizeChangedEvent
                        };
                        RaiseEvent(args);
                    }
                }
            }
        }
    }

    // --- LayoutTransform (WPF-parity): a transform that PARTICIPATES IN LAYOUT (unlike RenderTransform, which is render
    // only). The element measures/arranges its content in its OWN, untransformed space; the parent sees the transform's
    // bounding box, and LocalTransform (UIComponent) applies the transform to the rendered content. Everything below is a
    // no-op unless a non-identity LayoutTransform is set, so the null path is byte-identical to before. ------------------

    /// <summary>The LayoutTransform's matrix, when set and its 2D linear part is not identity (a pure translate or no
    /// transform is treated as none - it changes no size).</summary>
    private bool TryGetLayoutMatrix(out Matrix4x4 m)
    {
        var lt = LayoutTransform;
        if (lt == null) { m = default; return false; }
        m = lt.Matrix;
        return m.M11 != 1 || m.M22 != 1 || m.M12 != 0 || m.M21 != 0;
    }

    // One axis's absolute contribution to a bounding-box extent: |size * coeff|, but 0 (NOT NaN) when coeff is 0. A
    // StackPanel/WrapPanel measures with an INFINITE axis along its orientation, and inf * 0 = NaN would poison the whole
    // measure; a zero coefficient just means that axis doesn't contribute (size is always >= 0 here, so no sign to lose).
    private static double AxisContribution(double size, double coeff)
        => coeff == 0 ? 0 : size * Math.Abs(coeff);

    // Bounding-box size of the rect (0,0,size) under a matrix's 2D linear part (translation cannot change a size). Correct
    // for scale AND rotation: a rect's image is a parallelogram whose extents are the summed absolute axis contributions.
    private static Size TransformSize(Matrix4x4 m, Size s)
        => new(AxisContribution(s.Width, m.M11) + AxisContribution(s.Height, m.M21),
               AxisContribution(s.Width, m.M12) + AxisContribution(s.Height, m.M22));

    // The inverse map's bounding box - the local (untransformed) size whose transform fits the given outer size. Used to
    // turn an available/arrange size in the parent's space into the space the content is measured/arranged in.
    private static Size InverseTransformSize(Matrix4x4 m, Size s)
    {
        var det = m.M11 * m.M22 - m.M12 * m.M21;
        if (Math.Abs(det) < 1e-9) return s;   // singular (e.g. a zero scale) - nothing sensible to invert
        double i11 = m.M22 / det, i12 = -m.M12 / det, i21 = -m.M21 / det, i22 = m.M11 / det;
        return new(AxisContribution(s.Width, i11) + AxisContribution(s.Height, i21),
                   AxisContribution(s.Width, i12) + AxisContribution(s.Height, i22));
    }

    /// <summary>See <see cref="IMeasurableComponent.IsMeasureBoundary"/>. An element with BOTH Width and Height set
    /// (not Auto/NaN) has an externally-fixed DesiredSize that no child re-measure can change, so it is a measure
    /// boundary automatically - just like WPF. An auto-sized element (either dimension NaN) measures to fit its
    /// children, so a child's size change DOES propagate up. A virtualizing items host overrides this further.</summary>
    public virtual bool IsMeasureBoundary => !Double.IsNaN(Width) && !Double.IsNaN(Height);

    /// <summary>The one place that decides WHEN a parent has to hear that this element's contribution changed - both
    /// callers (a measure that came out a different size, and a visibility write, which no measure can reveal because a
    /// collapsed element reports zero from the instant the flag lands) go through here.
    /// <para>Not while the parent is measuring US: that cascade is already computing its own size from this one. Not a
    /// measure BOUNDARY (fixed size, or a virtualizing host whose extent is count x cell): propagating in is spurious
    /// and, running outside the panel's own layout mute, re-dirties it every iteration.</para></summary>
    internal override void NotifyParentOfContributionChange()
    {
        if (VisualParent is MeasurableUIComponent { IsMeasureValid: true, _measuring: false, IsMeasureBoundary: false } parent)
        {
            parent.InvalidateMeasure();
        }
    }

    public virtual void InvalidateMeasure()
    {
        if (Core.Diagnostics.LayoutTrace.Counting) Core.Diagnostics.LayoutTrace.Count(GetType(), "*any-measure*");

        // NO early return for an already-invalid node. The flag and the QUEUE are two different things: a node marked
        // invalid at a moment when it could not be enqueued - detached (the dirty queue belongs to the visual root), or
        // mid-pass - stays flagged forever, and returning here on the strength of that flag means the request to measure
        // it is dropped and nobody ever measures it again. Re-stating the request is cheap (the manager's queue is a set),
        // and it is the difference between a stale size healing on the next pass and never healing at all.
        // Measured: a folded tab whose label had been turned kept the footprint it had lying flat through every later
        // pass - its measure count simply stopped growing - while its own header presenter reported the turned size.
        IsMeasureValid = false;
        IsArrangeValid = false;
        InvalidateGeometryFromLayout();
        // KEEP _previousMeasure (cached constraint) and _previousArrange (slot): the manager re-measures/re-arranges this
        // node into them. The IsMeasureValid/IsArrangeValid=false flags, not nulled caches, are what force the re-run.

        if (_previousMeasure != null)
        {
            // Already measured: re-measure THIS node with its cached constraint. The manager propagates up to the parent
            // ONLY if this re-measure actually CHANGES our DesiredSize (i.e. the parent's measure genuinely depends on
            // us) - so an internal change that doesn't alter our outward size re-measures only this subtree, not the
            // whole chain up to the window.
            LayoutManager.For(this).InvalidateMeasure(this);
        }
        else if (VisualParent is IMeasurableComponent parent)
        {
            // Never measured yet (no cached constraint of our own): the nearest ancestor that owns one must measure us.
            // Propagate up the VISUAL tree (a template part has no logical parent, so a logical walk would stop at it -
            // the ScrollBar Track/thumb collapse).
            parent.InvalidateMeasure();
        }
        else
        {
            LayoutManager.For(this).InvalidateMeasure(this);
        }
    }
    public virtual void InvalidateArrange()
    {
        if (Core.Diagnostics.LayoutTrace.Counting) Core.Diagnostics.LayoutTrace.Count(GetType(), "*any-arrange*");

        // NO early return for an already-invalid node - the same reason spelled out in InvalidateMeasure: the flag and the
        // QUEUE are two different things. A node marked invalid at a moment when it could not be enqueued (collapsed, or
        // detached) would otherwise keep the flag forever and never be arranged again, because every later request sees
        // "already invalid" and drops itself. Re-stating the request is cheap - the manager's queue is a set.
        IsArrangeValid = false;
        InvalidateGeometryFromLayout();
        // _previousArrange is KEPT (the last correct slot) - see InvalidateMeasure. IsArrangeValid=false forces re-arrange.

        if (_previousArrange != null)
        {
            // Arrange is top-down: re-running THIS element's arrange into its own last correct slot re-distributes
            // correct rects to its children via ArrangeOverride - so an arrange-only change re-lays-out just this
            // subtree, not the whole tree, and never parks anything at the origin. (E.g. a ScrollBar's Value is
            // AffectsArrange on the Track, so the Track re-arranges itself into its slot and repositions the thumb -
            // no walk up to the window. The saved slot is also what stops a 0-desired template part collapsing.)
            LayoutManager.For(this).InvalidateArrange(this);
        }
        else if (VisualParent is IMeasurableComponent parent)
        {
            // Never arranged yet, so we own no slot: the nearest ancestor that does must arrange us. Propagate up the
            // VISUAL tree (a template part has no logical parent, so a logical walk would stop at it).
            parent.InvalidateArrange();
        }
        else
        {
            LayoutManager.For(this).InvalidateArrange(this);
        }
    }

    /// <summary>Re-registers layout work that was raised while this element was DETACHED, with the manager that now owns
    /// it.</summary>
    /// <remarks>
    /// The dirty FLAGS live on the element and survive a detach, but the dirty QUEUES belong to a visual root, and
    /// <see cref="LayoutManager.For"/> resolves them through <see cref="UIComponent.RootVisual"/> - which a detached
    /// element does not have. So an invalidation raised while detached is enqueued nowhere the layout pass will ever
    /// look, and re-attaching alone does not undo that: the element stays measure/arrange-invalid forever while its
    /// (clean) new ancestors short-circuit the measure cascade above it and never reach it.
    ///
    /// That is exactly a theme swap: re-templating the window detaches its whole content subtree, every element in it is
    /// then re-styled - and re-templated - while detached, and the invalidations that follow are lost. The window came
    /// back EMPTY until a resize, whose new constraint fails every Measure gate and so re-walks the tree by brute force.
    /// (WPF has the same seam and closes it the same way, in PropagateResumeLayout.)
    ///
    /// Only an element that owns a cached constraint/slot re-registers ITSELF - the same condition
    /// <see cref="InvalidateMeasure"/> uses to decide it can re-measure alone. One that has never been laid out has no
    /// constraint to re-measure with; it is measured by the parent that hosts it, as it always was.
    /// </remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!IsMeasureValid && _previousMeasure != null)
            LayoutManager.For(this).InvalidateMeasure(this);
        else if (!IsArrangeValid && _previousArrange != null)
            LayoutManager.For(this).InvalidateArrange(this);

        // A size worked out while DETACHED never reached the parent - the propagation travels the visual root's dirty
        // queue, and there was no root - so it must be told here, once, by the subtree ROOT only: everyone else's parent
        // is inside this same subtree and is being invalidated anyway (per node it cost 8.8 ms over 3351). Parked is
        // excluded: it returns whole and ParkedSubtree.Unpark decides whether its layout still holds.
        // e.Component is whoever the walk started from, so this is the "am I the root" test.
        if (IsParked || !ReferenceEquals(this, e.Component)) return;

        (VisualParent as IMeasurableComponent)?.InvalidateMeasure();
    }
}
