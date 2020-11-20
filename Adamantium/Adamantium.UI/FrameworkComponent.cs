using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.Core.Collections;

namespace Adamantium.UI
{
    public class FrameworkComponent : UIComponent, IName, IFrameworkComponent
    {
        public FrameworkComponent()
        { }

        public static readonly AdamantiumProperty TagProperty = AdamantiumProperty.Register(nameof(Tag),
           typeof(object), typeof(FrameworkComponent), new PropertyMetadata(null));

        public static readonly AdamantiumProperty NameProperty = AdamantiumProperty.Register(nameof(Name),
           typeof(String), typeof(FrameworkComponent), new PropertyMetadata(String.Empty));

        public static readonly AdamantiumProperty WidthProperty = AdamantiumProperty.Register(nameof(Width),
           typeof(Double), typeof(FrameworkComponent),
           new PropertyMetadata(Double.NaN, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));

        public static readonly AdamantiumProperty HeightProperty = AdamantiumProperty.Register(nameof(Height),
           typeof(Double), typeof(FrameworkComponent),
           new PropertyMetadata(Double.NaN, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));

        public static readonly AdamantiumProperty MinWidthProperty = AdamantiumProperty.Register(nameof(MinWidth),
           typeof(Double), typeof(FrameworkComponent),
           new PropertyMetadata((Double)0, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));

        public static readonly AdamantiumProperty MinHeightProperty = AdamantiumProperty.Register(nameof(MinHeight),
           typeof(Double), typeof(FrameworkComponent),
           new PropertyMetadata((Double)0, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));

        public static readonly AdamantiumProperty ActualWidthProperty = AdamantiumProperty.RegisterReadOnly(nameof(ActualWidth),
           typeof(Double), typeof(FrameworkComponent),
           new PropertyMetadata((Double)0));

        public static readonly AdamantiumProperty ActualHeightProperty = AdamantiumProperty.RegisterReadOnly(nameof(ActualHeight),
           typeof(Double), typeof(FrameworkComponent),
           new PropertyMetadata((Double)0));

        public static readonly AdamantiumProperty MaxWidthProperty = AdamantiumProperty.Register(nameof(MaxWidth),
           typeof(Double), typeof(FrameworkComponent),
           new PropertyMetadata(Double.PositiveInfinity, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));

        public static readonly AdamantiumProperty MaxHeightProperty = AdamantiumProperty.Register(nameof(MaxHeight),
           typeof(Double), typeof(FrameworkComponent),
           new PropertyMetadata(Double.PositiveInfinity, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));

        public static readonly AdamantiumProperty HorizontalAlignmentProperty = AdamantiumProperty.Register(nameof(HorizontalAlignment),
           typeof(HorizontalAlignment), typeof(FrameworkComponent), new PropertyMetadata(HorizontalAlignment.Stretch, PropertyMetadataOptions.AffectsArrange));

        public static readonly AdamantiumProperty VerticalAlignmentProperty = AdamantiumProperty.Register(nameof(VerticalAlignment),
           typeof(VerticalAlignment), typeof(FrameworkComponent), new PropertyMetadata(VerticalAlignment.Stretch, PropertyMetadataOptions.AffectsArrange));

        public static readonly AdamantiumProperty MarginProperty = AdamantiumProperty.Register(nameof(Margin),
           typeof(Thickness), typeof(FrameworkComponent), new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

        public static readonly AdamantiumProperty DataContextProperty = AdamantiumProperty.Register(nameof(DataContext),
           typeof(object), typeof(FrameworkComponent),
           new PropertyMetadata(null, PropertyMetadataOptions.Inherits, DataContextChangedCallBack));

        public static readonly RoutedEvent SizeChangedEvent = EventManager.RegisterRoutedEvent("SizeChanged",
           RoutingStrategy.Direct, typeof(SizeChangedEventHandler), typeof(FrameworkComponent));

        private static void DataContextChangedCallBack(DependencyComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
        {
            var o = adamantiumObject as FrameworkComponent;
            o?.DataContextChanged?.Invoke(o, e);
        }


        public object DataContext
        {
            get => GetValue(DataContextProperty);
            set => SetValue(DataContextProperty, value);
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

        public String Name
        {
            get => GetValue<String>(NameProperty);
            set => SetValue(NameProperty, value);
        }

        public object Tag
        {
            get => GetValue(TagProperty);
            set => SetValue(TagProperty, value);
        }

        public event AdamantiumPropertyChangedEventHandler DataContextChanged;

        private FrameworkComponent _parent;
        private TrackingCollection<FrameworkComponent> logicalChildren;

        public FrameworkComponent Parent => _parent;
        public IReadOnlyCollection<FrameworkComponent> LogicalChildrenCollection => LogicalChildren.AsReadOnly();


        protected TrackingCollection<FrameworkComponent> LogicalChildren
        {
            get
            {
                if (logicalChildren == null)
                {
                    var list = new TrackingCollection<FrameworkComponent>();
                    LogicalChildren = list;
                }
                return logicalChildren;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                if (logicalChildren != value)
                {
                    if (logicalChildren != null)
                    {
                        logicalChildren.CollectionChanged -= LogicalChildrenCollectionChanged;
                    }
                }

                logicalChildren = value;
                logicalChildren.CollectionChanged += LogicalChildrenCollectionChanged;
            }

        }

        private void LogicalChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    SetLogicalParent(e.NewItems.Cast<FrameworkComponent>());
                    break;

                case NotifyCollectionChangedAction.Remove:
                    ClearLogicalParent(e.OldItems.Cast<FrameworkComponent>());
                    break;

                case NotifyCollectionChangedAction.Replace:
                    ClearLogicalParent(e.OldItems.Cast<FrameworkComponent>());
                    SetLogicalParent(e.NewItems.Cast<FrameworkComponent>());
                    break;

                case NotifyCollectionChangedAction.Reset:
                    throw new NotSupportedException("Reset should not be signalled on LogicalChildren collection");
            }
        }

        private void SetLogicalParent(IEnumerable<FrameworkComponent> children)
        {
            foreach (var element in children)
            {
                element.SetParent(this);
            }
        }

        private void ClearLogicalParent(IEnumerable<FrameworkComponent> children)
        {
            foreach (var element in children)
            {
                if (element.Parent == this)
                {
                    element.SetParent(null);
                }
            }
        }

        /// <summary>
        /// Sets the control's logical parent.
        /// </summary>
        /// <param name="parent">The parent.</param>
        private void SetParent(FrameworkComponent parent)
        {
            var old = Parent;

            if (parent != old)
            {
                if (old != null && parent != null)
                {
                    throw new InvalidOperationException("The Control already has a parent.Parent Element is: " + Parent);
                }

                InheritanceParent = parent;
                _parent = parent;

                /*
                var root = FindStyleRoot(old);

                if (root != null)
                {
                   var e = new LogicalTreeAttachmentEventArgs(root);
                   OnDetachedFromLogicalTree(e);
                }

                root = FindStyleRoot(this);

                if (root != null)
                {
                   var e = new LogicalTreeAttachmentEventArgs(root);
                   OnAttachedToLogicalTree(e);
                }

                RaisePropertyChanged(ParentProperty, old, _parent, BindingPriority.LocalValue);
                */
            }
        }


        protected virtual void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
        { }

        protected virtual void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        { }

        /// <summary>
        /// Raised when the control is attached to a rooted logical tree.
        /// </summary>
        public event EventHandler<LogicalTreeAttachmentEventArgs> AttachedToLogicalTree;

        /// <summary>
        /// Raised when the control is detached from a rooted logical tree.
        /// </summary>
        public event EventHandler<LogicalTreeAttachmentEventArgs> DetachedFromLogicalTree;

        /// <summary>
        /// The default implementation of the control's measure pass.
        /// </summary>
        /// <param name="availableSize">The size available to the control.</param>
        /// <returns>The desired size for the control.</returns>
        /// <remarks>
        /// This method calls <see cref="MeasureOverride(Size)"/> which is probably the method you
        /// want to override in order to modify a control's arrangement.
        /// </remarks>
        protected sealed override Size MeasureCore(Size availableSize)
        {
            if (Visibility == Visibility.Visible || Visibility == Visibility.Hidden)
            {
                var margin = Margin;

                //TODO: This should be ignored by top level controls
                Size constrained = this.ApplyLayoutConstraints(availableSize).Deflate(margin);

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

                //TODO: This should be ignored by top level controls
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
        protected sealed override void ArrangeCore(Rect finalRect)
        {
            if (Visibility == Visibility.Visible || Visibility == Visibility.Hidden)
            {
                //TODO: Margin should be ignored by top level controls
                var margin = Margin;


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
                    if (RenderSize.Width != previousRenderSize.Width)
                    {
                        widthChanged = true;
                    }
                    if (RenderSize.Height != previousRenderSize.Height)
                    {
                        heightChanged = true;
                    }
                    if (widthChanged || heightChanged)
                    {
                        SizeChangedEventArgs args = new SizeChangedEventArgs(Bounds.Size, previousRenderSize, widthChanged,
                           heightChanged);
                        previousRenderSize = Bounds.Size;
                        args.RoutedEvent = SizeChangedEvent;
                        RaiseEvent(args);
                    }
                }
            }
        }

        public bool ApplyTemplate()
        {
            RaiseEvent(new RoutedEventArgs(LoadedEvent, this));

            return true;
        }
    }

    /// <summary>
    /// Provides helper methods needed for layout.
    /// </summary>
    public static class LayoutHelper
    {
        /// <summary>
        /// Calculates a control's size based on its <see cref="FrameworkComponent.Width"/>,
        /// <see cref="FrameworkComponent.Height"/>, <see cref="FrameworkComponent.MinWidth"/>,
        /// <see cref="FrameworkComponent.MaxWidth"/>, <see cref="FrameworkComponent.MinHeight"/> and
        /// <see cref="FrameworkComponent.MaxHeight"/>.
        /// </summary>
        /// <param name="element">The control.</param>
        /// <param name="constraints">The space available for the control.</param>
        /// <returns>The control's size.</returns>
        public static Size ApplyLayoutConstraints(this FrameworkComponent element, Size constraints)
        {
            double width = (element.Width > 0) ? element.Width : constraints.Width;
            double height = (element.Height > 0) ? element.Height : constraints.Height;
            width = Math.Min(width, element.MaxWidth);
            width = Math.Max(width, element.MinWidth);
            height = Math.Min(height, element.MaxHeight);
            height = Math.Max(height, element.MinHeight);
            return new Size(width, height);
        }
    }

}
