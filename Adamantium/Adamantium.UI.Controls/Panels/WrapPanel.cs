using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Panels;

public class WrapPanel : VirtualizingPanel
{
   // ---- Virtualized 2D state (items host) ----
   private const int Buffer = 1;        // extra lines on each side of the viewport
   private const int MaxCellPasses = 4; // bound the in-pass convergence of the auto-sized cell
   private double _cellFlow = 1;        // cell size along the flow axis
   private double _cellScroll = 1;      // cell size along the scroll (wrap) axis
   private int _columns = 1;            // items per line
   private int _lastFirstLine;          // remembered first visible line -> probe next pass
   private int _lastItemCount = -1;     // detect data changes -> re-establish the cached cell

   public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
      typeof(Orientation), typeof(WrapPanel), new PropertyMetadata(Orientation.Horizontal, PropertyMetadataOptions.AffectsMeasure|PropertyMetadataOptions.AffectsArrange));

   // AffectsMeasure (not just AffectsArrange): the virtualizing cell size is computed in MeasureVirtualized (SeedCell
   // reads ItemWidth/ItemHeight). If only arrange were invalidated, a slider-driven size change wouldn't recompute the
   // cell until the next measure - so tiles only resized on the next scroll. Measure re-runs -> cell + window rebuild.
   public static readonly AdamantiumProperty ItemWidthProperty = AdamantiumProperty.Register(nameof(ItemWidth),
      typeof(Double), typeof(WrapPanel), new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsMeasure|PropertyMetadataOptions.AffectsArrange));

   public static readonly AdamantiumProperty ItemHeightProperty = AdamantiumProperty.Register(nameof(ItemHeight),
      typeof(Double), typeof(WrapPanel), new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsMeasure|PropertyMetadataOptions.AffectsArrange));


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

         // The panel's cross-axis extent is the sum of LINE sizes (each line = the max child along the cross axis); it must
         // grow once per COMPLETED line, i.e. only when a child wraps to the next one (plus a final flush after the loop).
         // The previous code accumulated lineSize into desiredSize on EVERY child, so a single row of N items reported a
         // height of sum(item heights) instead of the row's height - inflating the panel ~N× (the huge empty WrapPanel).
         if (Orientation == Orientation.Horizontal)
         {
            if (lineSize.Width + childSize.Width > availableSize.Width)   // wrap (matches ArrangePlain's <= "fits")
            {
               desiredSize.Width = Math.Max(desiredSize.Width, lineSize.Width);
               desiredSize.Height += lineSize.Height;
               lineSize = childSize;
            }
            else
            {
               lineSize.Width += childSize.Width;
               lineSize.Height = Math.Max(lineSize.Height, childSize.Height);
            }
         }
         else
         {
            if (lineSize.Height + childSize.Height > availableSize.Height)
            {
               desiredSize.Height = Math.Max(desiredSize.Height, lineSize.Height);
               desiredSize.Width += lineSize.Width;
               lineSize = childSize;
            }
            else
            {
               lineSize.Height += childSize.Height;
               lineSize.Width = Math.Max(lineSize.Width, childSize.Width);
            }
         }
      }

      // Flush the final (in-progress) line.
      if (Orientation == Orientation.Horizontal)
      {
         desiredSize.Width = Math.Max(desiredSize.Width, lineSize.Width);
         desiredSize.Height += lineSize.Height;
      }
      else
      {
         desiredSize.Height = Math.Max(desiredSize.Height, lineSize.Height);
         desiredSize.Width += lineSize.Width;
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
         _lastItemCount = 0;
         return new Size();
      }

      var viewportFlow = horizontal ? availableSize.Width : availableSize.Height;
      var viewportScroll = horizontal ? availableSize.Height : availableSize.Width;

      // The data set changed -> the cached uniform cell may no longer represent the items; re-establish it.
      if (count != _lastItemCount)
      {
         _cellFlow = _cellScroll = 1;
         _lastItemCount = count;
      }

      // Seed the assumed cell so we have a sane column count before windowing (explicit ItemWidth/Height win).
      SeedCell(horizontal, count);

      // Resolve columns + the realized window and measure it; if a realized item is bigger than the assumed uniform
      // cell, grow the cell (only along axes the user didn't pin) and resolve again. This converges in a couple of passes
      // and then stays put across scrolls. The old code re-probed ONE variable-width item every pass, so the cell and
      // column count flickered -> the whole grid reflowed (overlapping/garbled rows) and every frame re-bound the window.
      int first = 0, last = -1;
      for (var pass = 0; pass < MaxCellPasses; pass++)
      {
         _columns = Math.Max(1, (int)Math.Floor(viewportFlow / _cellFlow));
         var lines = (count + _columns - 1) / _columns;

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
         var grew = false;
         for (var i = first; i <= last; i++)
         {
            var child = (IMeasurableComponent)RealizeInWindow(i);
            child.Measure(CellConstraint(horizontal));
            grew |= GrowCell(horizontal, child.DesiredSize);
         }
         if (!grew) break;
      }

      var totalLines = (count + _columns - 1) / _columns;
      var flowExtent = _columns * _cellFlow;
      var scrollExtent = totalLines * _cellScroll;
      return horizontal ? new Size(flowExtent, scrollExtent) : new Size(scrollExtent, flowExtent);
   }

   protected override void ArrangeVirtualized(Size finalSize, Vector2 offset)
   {
      var horizontal = Orientation == Orientation.Horizontal;
      var scrollOffset = horizontal ? offset.Y : offset.X;

      foreach (var index in Owner.ItemContainerGenerator.RealizedIndices.ToList())
      {
         if (Owner.ItemContainerGenerator.ContainerFromIndex(index) is not IMeasurableComponent container) continue;
         var line = index / _columns;
         var col = index % _columns;
         var flowPos = col * _cellFlow;
         var scrollPos = line * _cellScroll - scrollOffset;
         container.Arrange(horizontal
            ? new Rect(flowPos, scrollPos, _cellFlow, _cellScroll)
            : new Rect(scrollPos, flowPos, _cellScroll, _cellFlow));
      }
   }

   // Seed the assumed uniform cell. Explicit ItemWidth/ItemHeight are taken as-is; an unspecified axis is probed from a
   // representative item (the first one in view) - the window measure then grows it to fit. We keep whatever the cell
   // already holds (it only grows), so the column count stays stable across scrolls instead of flickering per pass.
   private void SeedCell(bool horizontal, int count)
   {
      var widthExplicit = !double.IsNaN(ItemWidth);
      var heightExplicit = !double.IsNaN(ItemHeight);
      if (widthExplicit) { if (horizontal) _cellFlow = Math.Max(1, ItemWidth); else _cellScroll = Math.Max(1, ItemWidth); }
      if (heightExplicit) { if (horizontal) _cellScroll = Math.Max(1, ItemHeight); else _cellFlow = Math.Max(1, ItemHeight); }
      if (widthExplicit && heightExplicit) return;

      var probeIndex = Math.Clamp(_lastFirstLine * Math.Max(1, _columns), 0, count - 1);
      var probe = (IMeasurableComponent)RealizeInWindow(probeIndex);
      probe.Measure(CellConstraint(horizontal));
      GrowCell(horizontal, probe.DesiredSize);
   }

   // Measure constraint for one item: an axis pinned by ItemWidth/ItemHeight is constrained to the cell; an unspecified
   // axis is left free so the item reports its natural size and we can grow the cell to fit it (text stays on one line).
   private Size CellConstraint(bool horizontal)
   {
      // BOTH axes pinned (the uniform-tile case, e.g. the slider-driven grid): the item's MEASURED size is never used -
      // GrowCell can't grow a pinned axis and ArrangeVirtualized forces every item to the exact cell. So measure with an
      // UNBOUNDED (constant) constraint instead of the cell: it doesn't change when the cell does, so each item's measure
      // gate SKIPS re-measuring the whole visible grid on every cell change (a slider drag). Only genuinely new/dirty
      // items measure; the rest just re-ARRANGE into the new cell. This is the fix for the resize re-measure storm.
      if (!double.IsNaN(ItemWidth) && !double.IsNaN(ItemHeight)) return Size.Infinity;

      var flow = (horizontal ? !double.IsNaN(ItemWidth) : !double.IsNaN(ItemHeight)) ? _cellFlow : double.PositiveInfinity;
      var scroll = (horizontal ? !double.IsNaN(ItemHeight) : !double.IsNaN(ItemWidth)) ? _cellScroll : double.PositiveInfinity;
      return horizontal ? new Size(flow, scroll) : new Size(scroll, flow);
   }

   // Grow the uniform cell to fit a measured item, but never along an axis the user pinned via ItemWidth/ItemHeight.
   private bool GrowCell(bool horizontal, Size desired)
   {
      var grew = false;
      var flowExplicit = horizontal ? !double.IsNaN(ItemWidth) : !double.IsNaN(ItemHeight);
      var scrollExplicit = horizontal ? !double.IsNaN(ItemHeight) : !double.IsNaN(ItemWidth);
      var flow = horizontal ? desired.Width : desired.Height;
      var scroll = horizontal ? desired.Height : desired.Width;
      if (!flowExplicit && flow > _cellFlow) { _cellFlow = flow; grew = true; }
      if (!scrollExplicit && scroll > _cellScroll) { _cellScroll = scroll; grew = true; }
      return grew;
   }
}