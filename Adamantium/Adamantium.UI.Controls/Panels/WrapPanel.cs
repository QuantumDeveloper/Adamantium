using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Panels;

public class WrapPanel : VirtualizingPanel
{
   // ---- Virtualized 2D state (items host) ----
   private const int Buffer = 1;        // extra lines on each side of the viewport
   private double _cellFlow = 1;        // cell size along the flow axis
   private double _cellScroll = 1;      // cell size along the scroll (wrap) axis
   private int _columns = 1;            // items per line
   private int _lastFirstLine;          // remembered first visible line -> probe next pass
   private Size _childConstraint;       // constraint the window was measured with -> re-measure in arrange if needed

   public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
      typeof(Orientation), typeof(WrapPanel), new PropertyMetadata(Orientation.Horizontal, PropertyMetadataOptions.AffectsMeasure|PropertyMetadataOptions.AffectsArrange));

   public static readonly AdamantiumProperty ItemWidthProperty = AdamantiumProperty.Register(nameof(ItemWidth),
      typeof(Double), typeof(WrapPanel), new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsArrange));

   public static readonly AdamantiumProperty ItemHeightProperty = AdamantiumProperty.Register(nameof(ItemHeight),
      typeof(Double), typeof(WrapPanel), new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsArrange));


   public Orientation Orientation
   {
      get => GetValue<Orientation>(OrientationProperty);
      set => SetValue(OrientationProperty, value);
   }

   public Double ItemWidth
   {
      get => GetValue<Double>(ItemWidthProperty);
      set => SetValue(ItemWidthProperty, value);
   }

   public Double ItemHeight
   {
      get => GetValue<Double>(ItemHeightProperty);
      set => SetValue(ItemHeightProperty, value);
   }

   public WrapPanel()
   { }

   // ---- Plain container layout (a WrapPanel used with explicit Children; behaviour unchanged) -----------------
   protected override Size MeasurePlain(Size availableSize)
   {
      Size desiredSize = new Size();
      Size lineSize = new Size();
      var itemWidth = ItemWidth;
      var itemHeight = ItemHeight;

      double childAvailableWidth = double.PositiveInfinity;
      double childAvailableHeight = double.PositiveInfinity;

      if (!double.IsNaN(Width))
      {
         childAvailableWidth = Width;
      }

      childAvailableWidth = Math.Min(childAvailableWidth, MaxWidth);
      childAvailableWidth = Math.Max(childAvailableWidth, MinWidth);

      if (!double.IsNaN(Height))
      {
         childAvailableHeight = Height;
      }

      childAvailableHeight = Math.Min(childAvailableHeight, MaxHeight);
      childAvailableHeight = Math.Max(childAvailableHeight, MinHeight);

      if (!Double.IsNaN(itemWidth))
      {
         childAvailableWidth = itemWidth;
      }

      if (!Double.IsNaN(itemHeight))
      {
         childAvailableHeight = itemHeight;
      }

      foreach (var child in Children)
      {
         child.Measure(new Size(childAvailableWidth, childAvailableHeight));

         var childSize = new Size(child.DesiredSize);
         if (!Double.IsNaN(itemWidth))
         {
            childSize.Width = ItemWidth;
         }

         if (!Double.IsNaN(itemHeight))
         {
            childSize.Height = itemHeight;
         }

         if (Orientation == Orientation.Horizontal)
         {
            if (lineSize.Width + childSize.Width < availableSize.Width)
            {
               lineSize.Width += childSize.Width;
               lineSize.Height = Math.Max(lineSize.Height, childSize.Height);
            }
            else //moving to next line
            {
               desiredSize.Width = Math.Max(lineSize.Width, availableSize.Width);
               desiredSize.Height += lineSize.Height;
               lineSize = childSize;
            }
            desiredSize.Width = Math.Max(lineSize.Width, desiredSize.Width);
            desiredSize.Height += lineSize.Height;
         }
         else
         {
            if (lineSize.Height + childSize.Height < availableSize.Height)
            {
               lineSize.Height += childSize.Height;
               lineSize.Width = Math.Max(lineSize.Width, childSize.Width);
            }
            else //moving to next line
            {
               desiredSize.Height = Math.Max(lineSize.Height, availableSize.Height);
               desiredSize.Width += lineSize.Width;
               lineSize = childSize;
            }
            desiredSize.Height = Math.Max(lineSize.Height, desiredSize.Height);
            desiredSize.Width += lineSize.Width;
         }
      }
      return desiredSize;
   }

   protected override Size ArrangePlain(Size finalSize)
   {
      double accumulated = 0;
      var lineSize = new Size();
      int firstChildInLineindex = 0;
      for (int i = 0; i < Children.Count; i++)
      {
         var child = Children[i];
         var childSize = new Size(child.DesiredSize);
         if (!Double.IsNaN(ItemWidth))
         {
            childSize.Width = ItemWidth;
         }
         if (!Double.IsNaN(ItemHeight))
         {
            childSize.Height = ItemHeight;
         }
         if (Orientation == Orientation.Horizontal)
         {
            if (lineSize.Width + childSize.Width <= finalSize.Width)
            {
               lineSize.Width += childSize.Width;
               lineSize.Height = Math.Max(lineSize.Height, childSize.Height);
            }
            else
            {
               var controlsInLine = GetControlsBetween(firstChildInLineindex, i);
               ArrangeLine(accumulated, lineSize.Height, controlsInLine);
               accumulated += lineSize.Height;
               lineSize = childSize;
               firstChildInLineindex = i;
            }
         }
         else
         {
            if (lineSize.Height + childSize.Height <= finalSize.Height)
            {
               lineSize.Height += childSize.Height;
               lineSize.Width = Math.Max(lineSize.Width, childSize.Width);
            }
            else
            {
               var controlsInLine = GetControlsBetween(firstChildInLineindex, i);
               ArrangeLine(accumulated, lineSize.Width, controlsInLine);
               accumulated += lineSize.Width;
               lineSize = childSize;
               firstChildInLineindex = i;
            }
         }
      }
      if (firstChildInLineindex < Children.Count)
      {
         var controlsInLine = GetControlsBetween(firstChildInLineindex, Children.Count);
         ArrangeLine(accumulated, Orientation == Orientation.Horizontal ? lineSize.Height : lineSize.Width,
            controlsInLine);
      }


      return finalSize;

   }

   private IEnumerable<IMeasurableComponent> GetControlsBetween(int first, int last)
   {
      return Children.Skip(first).Take(last - first);
   }

   private void ArrangeLine(double accumulated, double lineSize, IEnumerable<IMeasurableComponent> controls)
   {
      bool isHorizontal = (Orientation == Orientation.Horizontal);
      double accumulatedY = 0;
      foreach (var control in controls)
      {
         var childSize = new Size(control.DesiredSize);
         if (!Double.IsNaN(ItemWidth))
         {
            childSize.Width = ItemWidth;
         }
         if (!Double.IsNaN(ItemHeight))
         {
            childSize.Height = ItemHeight;
         }
         if (Orientation == Orientation.Horizontal)
         {
            var x = accumulatedY;
            var y = accumulated;
            var width = childSize.Width;
            var height = childSize.Height;
            control.Arrange(new Rect(x, y, width, height));
            accumulatedY += childSize.Width;
         }
         else
         {
            var x = accumulated;
            var y = accumulatedY;
            var width =  isHorizontal ? childSize.Width: lineSize;
            var height = isHorizontal ? lineSize : childSize.Height;
            control.Arrange(new Rect(x, y, width, height));
            accumulatedY += childSize.Height;
         }
      }
   }

   // ---- Virtualized 2D layout (items host): uniform cell -> only the visible grid window is realized -----------

   protected override Size MeasureVirtualized(Size availableSize, Vector2 offset)
   {
      var horizontal = Orientation == Orientation.Horizontal;
      var count = Owner.Items.Count;
      if (count == 0)
      {
         foreach (var c in Owner.ItemContainerGenerator.SetWindow(0, -1)) c.Visibility = Visibility.Collapsed;
         return new Size();
      }

      var viewportFlow = horizontal ? availableSize.Width : availableSize.Height;
      var viewportScroll = horizontal ? availableSize.Height : availableSize.Width;

      ResolveCell(horizontal, count);

      _columns = Math.Max(1, (int)Math.Floor(viewportFlow / _cellFlow));
      var lines = (count + _columns - 1) / _columns;

      int first, last;
      if (double.IsInfinity(viewportScroll))
      {
         OnNoViewport();
         first = 0;
         last = count - 1;
      }
      else
      {
         var scrollOffset = horizontal ? offset.Y : offset.X;
         var firstLine = Math.Max(0, (int)Math.Floor(scrollOffset / _cellScroll) - Buffer);
         var lastLine = Math.Min(lines - 1, (int)Math.Ceiling((scrollOffset + viewportScroll) / _cellScroll) + Buffer);
         _lastFirstLine = firstLine;
         first = firstLine * _columns;
         last = Math.Min(count - 1, (lastLine + 1) * _columns - 1);
      }

      // Reconcile the realized grid window to exactly [first,last] (rebind in place; hide only true surplus).
      foreach (var c in Owner.ItemContainerGenerator.SetWindow(first, last)) c.Visibility = Visibility.Collapsed;
      var childConstraint = horizontal ? new Size(_cellFlow, _cellScroll) : new Size(_cellScroll, _cellFlow);
      _childConstraint = childConstraint;
      for (var i = first; i <= last; i++)
         ((IMeasurableComponent)RealizeInWindow(i)).Measure(childConstraint);

      var flowExtent = _columns * _cellFlow;
      var scrollExtent = lines * _cellScroll;
      return horizontal ? new Size(flowExtent, scrollExtent) : new Size(scrollExtent, flowExtent);
   }

   protected override void ArrangeVirtualized(Size finalSize, Vector2 offset)
   {
      var horizontal = Orientation == Orientation.Horizontal;
      var scrollOffset = horizontal ? offset.Y : offset.X;

      foreach (var index in Owner.ItemContainerGenerator.RealizedIndices.ToList())
      {
         if (Owner.ItemContainerGenerator.ContainerFromIndex(index) is not IMeasurableComponent container) continue;
         // See StackPanel: re-measure a container left invalid by a rebind so Arrange positions it instead of bailing
         // (the layout driver would otherwise park the unpositioned container at the parent origin = the pile/overlap).
         if (!container.IsMeasureValid) container.Measure(_childConstraint);
         var line = index / _columns;
         var col = index % _columns;
         var flowPos = col * _cellFlow;
         var scrollPos = line * _cellScroll - scrollOffset;
         container.Arrange(horizontal
            ? new Rect(flowPos, scrollPos, _cellFlow, _cellScroll)
            : new Rect(scrollPos, flowPos, _cellScroll, _cellFlow));
      }
   }

   // Resolve the uniform cell: explicit ItemWidth/ItemHeight, else measure the first item (assume uniform).
   private void ResolveCell(bool horizontal, int count)
   {
      var probeIndex = Math.Clamp(_lastFirstLine * Math.Max(1, _columns), 0, count - 1);
      var probe = (IMeasurableComponent)RealizeInWindow(probeIndex);
      probe.Measure(new Size(
         double.IsNaN(ItemWidth) ? double.PositiveInfinity : ItemWidth,
         double.IsNaN(ItemHeight) ? double.PositiveInfinity : ItemHeight));

      var cellW = double.IsNaN(ItemWidth) ? probe.DesiredSize.Width : ItemWidth;
      var cellH = double.IsNaN(ItemHeight) ? probe.DesiredSize.Height : ItemHeight;
      _cellFlow = Math.Max(1, horizontal ? cellW : cellH);
      _cellScroll = Math.Max(1, horizontal ? cellH : cellW);
   }
}