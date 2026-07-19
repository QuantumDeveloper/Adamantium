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

    private static void WidthChangedCallBack(AdamantiumComponent adamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        if (adamantiumComponent is not MeasurableUIComponent o) return;
        Size old = default;
        // Skip the boundary transitions where a value is first seeded from - or cleared back to - UnsetValue (a trigger
        // or style setter being removed): there is no concrete double to report, and the cast below would throw. Layout
        // re-measures on its own (Width AffectsMeasure), so no SizeChanged event is owed for the clear.
        if (e.OldValue == AdamantiumProperty.UnsetValue || e.NewValue == AdamantiumProperty.UnsetValue)
            return;

        old.Width = (double) e.OldValue;
        old.Height = o.Height;
            
        var newSize = new Size((double)e.NewValue, o.Height);
        var args = new SizeChangedEventArgs(old, newSize, true, false);
        args.RoutedEvent = SizeChangedEvent;
        o.OnSizeChanged(args);
        o.RaiseEvent(args);
    }
        
    private static void HeightChangedCallBack(AdamantiumComponent adamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(adamantiumComponent is MeasurableUIComponent o)) return;
        // See WidthChangedCallBack: ignore the UnsetValue boundary (a trigger/style value being seeded or cleared).
        if (e.OldValue == AdamantiumProperty.UnsetValue || e.NewValue == AdamantiumProperty.UnsetValue)
            return;

        var old = new Size(o.Width, (double)e.OldValue);
        var newSize = new Size(o.Width, (double)e.NewValue);
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

    public bool IsArrangeValid { get; private set; }

    public Size DesiredSize { get; private set; }

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
        if (Double.IsNaN(availableSize.Width) || Double.IsNaN(availableSize.Height))
        {
            throw new InvalidOperationException("Cannot call Measure using a size with NaN values.");
        }

        if (force || !IsMeasureValid || _previousMeasure != availableSize)
        {
            TotalMeasureCores++;
            IsMeasureValid = true;
            IsArrangeValid = false;
            IsGeometryValid = false;

            var desiredSize = MeasureCore(availableSize).Constrain(availableSize);

            if (IsInvalidSize(desiredSize))
            {
                throw new InvalidOperationException("Invalid size returned for Measure.");
            }

            DesiredSize = desiredSize;
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
            ArrangeCore(rect);
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
                
            // IWindow is top level control. Constraints should be ignored by top level controls
            // because it will lead to incorrect measurements
            if (this is IWindow)
            {
                constrained = availableSize;
                margin = new Thickness(0);
            }
            else
            {
                constrained = this.ApplyLayoutConstraints(availableSize.Deflate(margin));
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

            return NonNegative(new Size(width, height).Inflate(margin));
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

            size = this.ApplyLayoutConstraints(size);

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

            size = ArrangeOverride(size).Constrain(size);

            // A size change must re-run OnRender: a control that first rendered at a STALE size (e.g. 0x0 while still
            // unarranged - which happens for content built during a measure pass, like a tab body added via a
            // ContentPresenter/DataTemplate) cached that geometry and, being "geometry-valid", would never redraw at the
            // new size - its fill rect stays 0x0 = invisible. Invalidate the render geometry so the render pass re-records
            // it at the arranged size. Measure already does this (MeasureCore); arrange must too when the size changes.
            var renderSizeChanged = !MathHelper.NearEqual(RenderSize.Width, size.Width)
                                    || !MathHelper.NearEqual(RenderSize.Height, size.Height);

            ActualWidth = size.Width;
            ActualHeight = size.Height;
            RenderSize = size;

            if (renderSizeChanged) IsGeometryValid = false;

            switch (HorizontalAlignment)
            {
                // Stretch anchors at the START, like Left. When the element fills the slot the offset is 0 either way;
                // when it returns LESS than the slot (a control that can't stretch - e.g. a CheckBox's content stack)
                // it must stay at the slot origin, not drift to the middle. Grouping Stretch with Center slid such
                // content to the centre of its parent the moment the element stopped filling.
                case HorizontalAlignment.Left:
                case HorizontalAlignment.Stretch:
                    size.Width = Math.Min(sizeMinusMargins.Width, ActualWidth);
                    break;
                case HorizontalAlignment.Center:
                    originX += (sizeMinusMargins.Width - size.Width) / 2;
                    clipOriginX = Math.Max(originX, finalRect.X + margin.Left);
                    size.Width = Math.Min(sizeMinusMargins.Width, ActualWidth);
                    break;
                case HorizontalAlignment.Right:
                    originX += sizeMinusMargins.Width - size.Width;
                    clipOriginX = Math.Max(originX, margin.Left);
                    size.Width = Math.Min(sizeMinusMargins.Width, ActualWidth);
                    break;
            }

            switch (VerticalAlignment)
            {
                case VerticalAlignment.Top:
                case VerticalAlignment.Stretch:
                    size.Height = Math.Min(sizeMinusMargins.Height, ActualHeight);
                    break;
                case VerticalAlignment.Center:
                    originY += (sizeMinusMargins.Height - size.Height) / 2;
                    clipOriginY = Math.Max(originY, finalRect.Y + margin.Top);
                    size.Height = Math.Min(sizeMinusMargins.Height, ActualHeight);
                    break;
                case VerticalAlignment.Bottom:
                    originY += sizeMinusMargins.Height - size.Height;
                    clipOriginY = Math.Max(originY, finalRect.Y);
                    size.Height = Math.Min(sizeMinusMargins.Height, ActualHeight);
                    break;
            }

            if (UseLayoutRounding)
            {
                originX = Math.Floor(originX);
                originY = Math.Floor(originY);
            }

            ClipRectangle = new Rect(clipOriginX, clipOriginY,
                size.Width, size.Height);

            var newBounds = new Rect(originX, originY, ActualWidth, ActualHeight);
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
                    var args = new SizeChangedEventArgs(
                        previousRenderSize, 
                        Bounds.Size,
                        widthChanged,
                        heightChanged);
                    previousRenderSize = Bounds.Size;
                    args.RoutedEvent = SizeChangedEvent;
                    RaiseEvent(args);
                }
            }
        }
    }

    /// <summary>See <see cref="IMeasurableComponent.IsMeasureBoundary"/>. An element with BOTH Width and Height set
    /// (not Auto/NaN) has an externally-fixed DesiredSize that no child re-measure can change, so it is a measure
    /// boundary automatically - just like WPF. An auto-sized element (either dimension NaN) measures to fit its
    /// children, so a child's size change DOES propagate up. A virtualizing items host overrides this further.</summary>
    public virtual bool IsMeasureBoundary => !Double.IsNaN(Width) && !Double.IsNaN(Height);

    public virtual void InvalidateMeasure()
    {
        if (!IsMeasureValid) return;

        IsMeasureValid = false;
        IsArrangeValid = false;
        IsGeometryValid = false;
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
        if (!IsArrangeValid) return;

        IsArrangeValid = false;
        IsGeometryValid = false;
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
    }
}