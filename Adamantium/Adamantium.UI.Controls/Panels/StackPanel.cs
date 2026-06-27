using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Panels;

public class StackPanel : VirtualizingPanel
{
   // Realize a couple of extra items on each side of the viewport so a fast scroll doesn't flash empty rows.
   private const int Buffer = 2;

   private double _itemExtent = 1;   // measured (uniform) item size along the stacking axis
   private int _lastFirst;           // remembered window start -> the probe index next pass
   private Size _childConstraint;    // constraint the window was measured with -> re-measure in arrange if needed

   public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
      typeof(Orientation), typeof(StackPanel),
      new PropertyMetadata(Orientation.Horizontal,
         PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));


   public Orientation Orientation
   {
      get => GetValue<Orientation>(OrientationProperty);
      set => SetValue(OrientationProperty, value);
   }

   // ---- Plain container layout (unchanged behaviour: a StackPanel used with explicit Children) ------------------

   protected override Size MeasurePlain(Size availableSize)
   {
      double childAvailableWidth = double.PositiveInfinity;
      double childAvailableHeight = double.PositiveInfinity;

      if (Orientation == Orientation.Vertical)
      {
         childAvailableWidth = availableSize.Width;

         if (!double.IsNaN(Width))
         {
            childAvailableWidth = Width;
         }

         childAvailableWidth = Math.Min(childAvailableWidth, MaxWidth);
         childAvailableWidth = Math.Max(childAvailableWidth, MinWidth);
      }
      else
      {
         childAvailableHeight = availableSize.Height;

         if (!double.IsNaN(Height))
         {
            childAvailableHeight = Height;
         }

         childAvailableHeight = Math.Min(childAvailableHeight, MaxHeight);
         childAvailableHeight = Math.Max(childAvailableHeight, MinHeight);
      }

      double measuredWidth = 0;
      double measuredHeight = 0;


      foreach (var child in Children)
      {
         child.Measure(new Size(childAvailableWidth, childAvailableHeight));
         Size size = child.DesiredSize;

         if (Orientation == Orientation.Vertical)
         {
            measuredHeight += size.Height;
            measuredWidth = Math.Max(measuredWidth, size.Width);
         }
         else
         {
            measuredWidth += size.Width;
            measuredHeight = Math.Max(measuredHeight, size.Height);
         }
      }

      return new Size(measuredWidth, measuredHeight);
   }

   protected override Size ArrangePlain(Size finalSize)
   {
      var horizontal = Orientation == Orientation.Horizontal;

      // Cross-axis = the CONTENT extent (DesiredSize: the tallest/widest child, or an explicit Width/Height), clamped to
      // the slot - never the full slot on its own. A stack only occupies what it stacks, so it must not report (and
      // therefore hit-test) cross space it doesn't use: a horizontal Stretch stack otherwise swallowed the whole window
      // height for input, blocking everything beneath it. Children are still given the full cross extent so they can
      // align within it. (Stacking axis = the running content sum - a stack never fills along its orientation either.)
      double cross = horizontal
         ? Math.Min(finalSize.Height, DesiredSize.Height)
         : Math.Min(finalSize.Width, DesiredSize.Width);

      double main = 0;
      foreach (var child in Children)
      {
         if (horizontal)
         {
            child.Arrange(new Rect(main, 0, child.DesiredSize.Width, cross));
            main += child.DesiredSize.Width;
         }
         else
         {
            child.Arrange(new Rect(0, main, cross, child.DesiredSize.Height));
            main += child.DesiredSize.Height;
         }
      }

      return horizontal ? new Size(main, cross) : new Size(cross, main);
   }

   // ---- Virtualized layout (items host): realize only the visible window, scroll along Orientation -------------

   protected override Size MeasureVirtualized(Size availableSize, Vector2 offset)
   {
      var vertical = Orientation == Orientation.Vertical;
      var count = Owner.Items.Count;
      if (count == 0)
      {
         foreach (var c in Owner.ItemContainerGenerator.SetWindow(0, -1)) c.Visibility = Visibility.Collapsed;
         return new Size();
      }

      var crossAvailable = vertical ? availableSize.Width : availableSize.Height;
      var mainViewport = vertical ? availableSize.Height : availableSize.Width;
      var childConstraint = vertical
         ? new Size(crossAvailable, double.PositiveInfinity)
         : new Size(double.PositiveInfinity, crossAvailable);
      _childConstraint = childConstraint;

      // Probe a representative item for the (uniform) main-axis extent. Reuse the last window start as the probe so the
      // estimate tracks the items actually on screen.
      var probe = (IMeasurableComponent)RealizeInWindow(Math.Clamp(_lastFirst, 0, count - 1));
      probe.Measure(childConstraint);
      _itemExtent = Math.Max(1, vertical ? probe.DesiredSize.Height : probe.DesiredSize.Width);

      int first, last;
      if (double.IsInfinity(mainViewport))
      {
         OnNoViewport();
         first = 0;
         last = count - 1;
      }
      else
      {
         var mainOffset = vertical ? offset.Y : offset.X;
         first = Math.Max(0, (int)Math.Floor(mainOffset / _itemExtent) - Buffer);
         last = Math.Min(count - 1, (int)Math.Ceiling((mainOffset + mainViewport) / _itemExtent) + Buffer);
      }
      _lastFirst = first;

      // Reconcile the realized set to exactly [first,last]: containers leaving the window are rebound in place to the
      // ones entering (no collapse/null churn). Only true surplus (window shrank, e.g. resize/list edge) is hidden.
      foreach (var c in Owner.ItemContainerGenerator.SetWindow(first, last)) c.Visibility = Visibility.Collapsed;

      double crossMax = 0;
      for (var i = first; i <= last; i++)
      {
         var container = (IMeasurableComponent)RealizeInWindow(i);
         container.Measure(childConstraint);
         crossMax = Math.Max(crossMax, vertical ? container.DesiredSize.Width : container.DesiredSize.Height);
      }

      var mainExtent = count * _itemExtent;
      return vertical ? new Size(crossMax, mainExtent) : new Size(mainExtent, crossMax);
   }

   protected override void ArrangeVirtualized(Size finalSize, Vector2 offset)
   {
      var vertical = Orientation == Orientation.Vertical;
      var cross = vertical ? finalSize.Width : finalSize.Height;
      var mainOffset = vertical ? offset.Y : offset.X;

      foreach (var index in System.Linq.Enumerable.ToList(Owner.ItemContainerGenerator.RealizedIndices))
      {
         if (Owner.ItemContainerGenerator.ContainerFromIndex(index) is not IMeasurableComponent container) continue;
         // A rebind during measure can leave a container's measure invalid (its propagation up to the panel is muted by
         // the _inLayout guard, so the panel itself stays valid and may not re-run MeasureVirtualized). Arrange bails on
         // an invalid measure, leaving the container unpositioned - and the layout driver then parks it at the parent
         // origin (0,0), which is the pile/overlap at the top. Re-measure it here so Arrange actually positions it.
         if (!container.IsMeasureValid) container.Measure(_childConstraint);
         var main = index * _itemExtent - mainOffset;
         container.Arrange(vertical
            ? new Rect(0, main, cross, _itemExtent)
            : new Rect(main, 0, _itemExtent, cross));
      }
   }
}
