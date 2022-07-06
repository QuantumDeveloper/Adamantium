using System;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

public class MeasurableUIComponent : ObservableUIComponent, IName, IMeasurableComponent
{
    private Size? _previousMeasure;
    private Rect? _previousArrange;

    static MeasurableUIComponent()
    {
        SizeChangedEvent.RegisterClassHandler<UIComponent>(new SizeChangedEventHandler(SizeChangedHandler));
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

    public static readonly AdamantiumProperty UseLayoutRoundingProperty = AdamantiumProperty.Register(nameof(UseLayoutRounding),
        typeof(Boolean), typeof(MeasurableUIComponent), new PropertyMetadata(false, PropertyMetadataOptions.AffectsArrange));
    
    public static readonly RoutedEvent SizeChangedEvent = 
        EventManager.RegisterRoutedEvent("SizeChangedEvent",
            RoutingStrategy.Bubble, typeof(SizeChangedEventHandler), typeof(UIComponent));
    
    public event SizeChangedEventHandler SizeChanged
    {
        add => AddHandler(SizeChangedEvent, value);
        remove => RemoveHandler(SizeChangedEvent, value);
    }

    private static void WidthChangedCallBack(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(adamantiumObject is MeasurableUIComponent o)) return;
        Size old = default;
        if (e.OldValue == AdamantiumProperty.UnsetValue)
            return;
            
        old.Width = (double) e.OldValue;
        old.Height = o.Height;
            
        var newSize = new Size((double)e.NewValue, o.Height);
        var args = new SizeChangedEventArgs(old, newSize, true, false);
        args.RoutedEvent = SizeChangedEvent;
        o.RaiseEvent(args);
    }
        
    private static void HeightChangedCallBack(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
    {
        if (!(adamantiumObject is MeasurableUIComponent o)) return;
        if (e.OldValue == AdamantiumProperty.UnsetValue)
            return;
            
        var old = new Size(o.Width, (double)e.OldValue);
        var newSize = new Size(o.Width, (double)e.NewValue);
        var args = new SizeChangedEventArgs(old, newSize, false, true);
        args.RoutedEvent = SizeChangedEvent;
        o?.RaiseEvent(args);
    }
    
    private static void SizeChangedHandler(object sender, SizeChangedEventArgs e)
    {
        var measurable = sender as MeasurableUIComponent;
        measurable?.OnSizeChanged(e);
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
        
        foreach (var visual in LogicalChildren)
        {
            var child = (MeasurableUIComponent)visual;
            child.Measure(availableSize);
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }

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
        if (Double.IsNaN(availableSize.Width) || Double.IsNaN(availableSize.Height))
        {
            throw new InvalidOperationException("Cannot call Measure using a size with NaN values.");
        }

        if (force || !IsMeasureValid || _previousMeasure != availableSize)
        {
            IsMeasureValid = true;
            IsArrangeValid = false;
            IsGeometryValid = false;

            var desiredSize = MeasureCore(availableSize).Constrain(availableSize);

            if (IsInvalidSize(desiredSize))
            {
                throw new InvalidOperationException("Invalid size returned for Measure.");
            }

            DesiredSize = desiredSize;
            _previousMeasure = DesiredSize;
        }
    }

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
        
        foreach (var visual in LogicalChildren)
        {
            var child = (IMeasurableComponent)visual;
            child.Arrange(new Rect(finalSize));
        }

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
        if (IsInvalidRect(rect))
        {
            throw new InvalidOperationException("Invalid Arrange rectangle.");
        }

        // If the measure was invalidated during an arrange pass, wait for the measure pass to
        // be re-run.
        if (!IsMeasureValid)
        {
            return;
        }

        if (force || !IsArrangeValid || _previousArrange != rect)
        {
            IsArrangeValid = true;
            ArrangeCore(rect);
            _previousArrange = rect;
        }
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
        if (Visibility == Visibility.Visible || Visibility == Visibility.Hidden)
        {
            var margin = Margin;
                
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
                constrained = this.ApplyLayoutConstraints(availableSize).Deflate(margin);
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
        if (Visibility == Visibility.Visible || Visibility == Visibility.Hidden)
        {
            var margin = Margin;
                
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

            if (HorizontalAlignment != HorizontalAlignment.Stretch)
            {
                size.Width = Math.Min(size.Width, DesiredSize.Width);
            }

            if (VerticalAlignment != VerticalAlignment.Stretch)
            {
                size.Height = Math.Min(size.Height, DesiredSize.Height);
            }

            size = this.ApplyLayoutConstraints(size);
                
            if (this is IWindow)
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

            ActualWidth = size.Width;
            ActualHeight = size.Height;
            RenderSize = size;

            switch (HorizontalAlignment)
            {
                case HorizontalAlignment.Left:
                    size.Width = Math.Min(sizeMinusMargins.Width, ActualWidth);
                    break;
                case HorizontalAlignment.Center:
                case HorizontalAlignment.Stretch:
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
                    size.Height = Math.Min(sizeMinusMargins.Height, ActualHeight);
                    break;
                case VerticalAlignment.Center:
                case VerticalAlignment.Stretch:
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

            Bounds = new Rect(originX, originY, ActualWidth, ActualHeight);

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
                    SizeChangedEventArgs args = new SizeChangedEventArgs(previousRenderSize, Bounds.Size, widthChanged,
                        heightChanged);
                    previousRenderSize = Bounds.Size;
                    args.RoutedEvent = SizeChangedEvent;
                    RaiseEvent(args);
                }
            }
        }
    }

    public void InvalidateMeasure()
    {
        IsMeasureValid = false;
        IsArrangeValid = false;
        IsGeometryValid = false;
        _previousMeasure = null;
        _previousArrange = null;

        if (LogicalParent is IMeasurableComponent parent)
        {
            parent?.InvalidateMeasure();
        }
    }

    public void InvalidateArrange()
    {
        IsArrangeValid = false;

        _previousArrange = null;

        if (LogicalParent is IMeasurableComponent parent)
        {
            parent?.InvalidateArrange();
        }

    }

}