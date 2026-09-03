using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Panels;

public class StackPanel : VirtualizingPanel
{
   // Realize a couple of extra items on each side of the viewport so a fast scroll doesn't flash empty rows.
   private const int Buffer = 2;

   private double _itemExtent = 1;   // measured (uniform) item size along the stacking axis
   private int _lastFirst;           // remembered window start -> the probe index next pass
   private double _lastViewport;     // last FINITE scroll-axis viewport -> window size when a measure comes in infinite (tab-entry)
   private const double DefaultViewport = 1080.0;   // fallback viewport (px) before any real one is known
   private const int ParallelArrangeThreshold = 64;   // arrange tiles across cores only above this many realized (else thread overhead > win)
   private readonly List<int> _arrangeIndexBuf = [];  // reused each arrange - no per-frame List alloc over the window
   private readonly List<double> _itemStarts = [];    // non-virtualized: cumulative main-axis start per item (mixed heights)

   public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
      typeof(Orientation), typeof(StackPanel),
      new PropertyMetadata(Orientation.Vertical,
         PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

   /// <summary>The gap BETWEEN children along the stacking axis - not after the last one. The container's business
   /// rather than each child's: a margin per child repeats one number, trails a gap off the last one, and doubles up
   /// when a middle child is collapsed. Here a collapsed child takes neither slot nor gap.</summary>
   public static readonly AdamantiumProperty SpacingProperty = AdamantiumProperty.Register(nameof(Spacing),
      typeof(double), typeof(StackPanel),
      new PropertyMetadata(0.0,
         PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

   /// <summary>Item extent PLUS the gap - one item's start to the next. EVERY place that turns an index into a position
   /// uses this and not the extent: the realized window, a slot's rectangle, the drop target under the pointer, and the
   /// answer for an item that is not realized. One of them disagreeing is a row drawn where the hit-test is not.
   /// </summary>
   private double Pitch => _itemExtent + Math.Max(0, Spacing);


   /// <summary>Items stack at a uniform extent, so where the n-th one sits is arithmetic - and stays answerable for an
   /// item that has been virtualized away, which is exactly when someone needs to scroll to it.</summary>
   public override bool TryGetItemRect(int index, out Rect rect)
   {
      rect = default;
      if (!IsItemsHost || _itemExtent <= 0 || index < 0) return false;

      var vertical = Orientation == Orientation.Vertical;
      var along = index * Pitch;
      rect = vertical
         ? new Rect(0, along, RenderSize.Width, _itemExtent)
         : new Rect(along, 0, _itemExtent, RenderSize.Height);
      return true;
   }

   /// <summary>Along the stack an arrow is the next child; ACROSS it there is nothing - a line of children has no
   /// second dimension - so the answer is null and the question goes to whatever holds this stack.</summary>
   public override IUIComponent Navigate(IUIComponent from, FocusNavigationDirection direction)
   {
      if (!IsArrow(direction)) return base.Navigate(from, direction);
      if (IsVertical(direction) != (Orientation == Orientation.Vertical)) return null;

      return Neighbour(from, IsForward(direction));
   }

   public Orientation Orientation
   {
      get => GetValue<Orientation>(OrientationProperty);
      set => SetValue(OrientationProperty, value);
   }

   public double Spacing
   {
      get => GetValue<double>(SpacingProperty);
      set => SetValue(SpacingProperty, value);
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
      var gap = Math.Max(0, Spacing);
      var stacked = 0;   // children that actually take a slot - a collapsed one takes neither slot nor gap

      foreach (var child in Children)
      {
         child.Measure(new Size(childAvailableWidth, childAvailableHeight));
         if (child.Visibility == Visibility.Collapsed) continue;
         Size size = child.DesiredSize;
         stacked++;

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

      // BETWEEN, so one fewer gap than children and none at all for a single child.
      var gaps = gap * Math.Max(0, stacked - 1);
      if (Orientation == Orientation.Vertical) measuredHeight += gaps;
      else measuredWidth += gaps;

      return new Size(measuredWidth, measuredHeight);
   }

   protected override Size ArrangePlain(Size finalSize)
   {
      var horizontal = Orientation == Orientation.Horizontal;

      // Cross-axis = the CONTENT extent (DesiredSize: the tallest/widest child, or an explicit Width/Height), clamped to
      // the slot - never the full slot on its own. A stack only occupies what it stacks, so it must not REPORT (and
      // therefore hit-test) cross space it doesn't use: a horizontal Stretch stack otherwise swallowed the whole window
      // height for input, blocking everything beneath it. That clamp is the panel's RETURN value below.
      double cross = horizontal
         ? Math.Min(finalSize.Height, DesiredSize.Height)
         : Math.Min(finalSize.Width, DesiredSize.Width);

      // Children, however, get the FULL cross slot so a Stretch child fills the panel's width (a full-width row - a
      // TreeView/list item that spans the viewport, clickable + highlight edge-to-edge); a non-Stretch child sizes to its
      // content within that slot (its own Arrange aligns it). Clamping the CHILD to `cross` - as this used to - is what
      // pinned every Stretch child to content width. Guard infinity (an unconstrained arrange) back to the content extent.
      double childCross = horizontal
         ? (double.IsInfinity(finalSize.Height) ? cross : finalSize.Height)
         : (double.IsInfinity(finalSize.Width) ? cross : finalSize.Width);

      double main = 0;
      var gap = Math.Max(0, Spacing);
      var placed = false;
      foreach (var child in Children)
      {
         if (child.Visibility == Visibility.Collapsed) continue;
         if (placed) main += gap;   // between the previous child and this one, never after the last
         placed = true;

         if (horizontal)
         {
            child.Arrange(new Rect(main, 0, child.DesiredSize.Width, childCross));
            main += child.DesiredSize.Width;
         }
         else
         {
            child.Arrange(new Rect(0, main, childCross, child.DesiredSize.Height));
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
         foreach (var c in Owner.ItemContainerGenerator.SetWindow(0, -1)) ParkContainer(c);
         return new Size();
      }

      var crossAvailable = vertical ? availableSize.Width : availableSize.Height;
      var mainViewport = vertical ? availableSize.Height : availableSize.Width;
      var childConstraint = vertical
         ? new Size(crossAvailable, double.PositiveInfinity)
         : new Size(double.PositiveInfinity, crossAvailable);

      // Non-virtualized host (a small mixed-height menu): realize + measure EVERY item at its OWN extent and stack them by
      // cumulative offset, so a short row (a separator) takes only its own height instead of a uniform cell.
      if (!IsVirtualizing)
      {
         foreach (var c in Owner.ItemContainerGenerator.SetWindow(0, count - 1)) ParkContainer(c);   // realize all
         _itemStarts.Clear();
         double runningMain = 0, crossMaxAll = 0;
         for (var i = 0; i < count; i++)
         {
            var container = (IMeasurableComponent)RealizeInWindow(i);
            container.Measure(childConstraint);
            if (i > 0) runningMain += Math.Max(0, Spacing);   // between rows only
            _itemStarts.Add(runningMain);
            runningMain += vertical ? container.DesiredSize.Height : container.DesiredSize.Width;
            crossMaxAll = Math.Max(crossMaxAll, vertical ? container.DesiredSize.Width : container.DesiredSize.Height);
         }
         return vertical ? new Size(crossMaxAll, runningMain) : new Size(runningMain, crossMaxAll);
      }

      // Probe a representative item for the (uniform) main-axis extent. Reuse the last window start as the probe so the
      // estimate tracks the items actually on screen. KEEP the last good size if the probe momentarily measures to nothing
      // (a recycled container mid-rebind can report a stale/zero DesiredSize): collapsing _itemExtent to 1 shrinks the whole
      // extent (count*1), which then mis-clamps the scroll offset. Offset-baked arrange tolerated that (arrange + reported
      // offset stay same-pass consistent); transform-only scroll does NOT (the presenter's translation desyncs from the
      // reported offset across a flip). Re-probe freely on any POSITIVE measure so a real size change still tracks.
      var probe = (IMeasurableComponent)RealizeInWindow(Math.Clamp(_lastFirst, 0, count - 1));
      probe.Measure(childConstraint);
      var probeExtent = vertical ? probe.DesiredSize.Height : probe.DesiredSize.Width;
      if (probeExtent > 0) _itemExtent = probeExtent;

      // A ScrollViewer measures its content UNCONSTRAINED on the scroll axis to learn the extent, so we get an infinite
      // mainViewport on the first measure after (re)entering a view - before arrange sets the real viewport. Realizing all
      // `count` items then (the old OnNoViewport path) rebuilt the whole list every tab-entry (the freeze). Instead realize
      // a window sized to the last real viewport (or a default screenful) and still return the full extent below - the next
      // measure with the real viewport corrects it. O(count) freeze -> O(viewport).
      double effectiveViewport;
      if (double.IsInfinity(mainViewport))
      {
         OnNoViewport();
         effectiveViewport = _lastViewport > 0 ? _lastViewport : DefaultViewport;
      }
      else
      {
         effectiveViewport = mainViewport;
         _lastViewport = mainViewport;
      }

      var mainOffset = vertical ? offset.Y : offset.X;
      var pitch = Pitch;
      var first = Math.Max(0, (int)Math.Floor(mainOffset / pitch) - Buffer);
      var last = Math.Min(count - 1, (int)Math.Ceiling((mainOffset + effectiveViewport) / pitch) + Buffer);
      _lastFirst = first;

      // Reconcile the realized set to exactly [first,last]: containers leaving the window are rebound in place to the
      // ones entering (no collapse/null churn). Only true surplus (window shrank, e.g. resize/list edge) is hidden.
      foreach (var c in Owner.ItemContainerGenerator.SetWindow(first, last)) ParkContainer(c);

      double crossMax = 0;
      for (var i = first; i <= last; i++)
      {
         var container = (IMeasurableComponent)RealizeInWindow(i);
         container.Measure(childConstraint);
         crossMax = Math.Max(crossMax, vertical ? container.DesiredSize.Width : container.DesiredSize.Height);
      }

      // count pitches less the trailing gap: the last item ends the extent, the gap after it does not exist.
      var mainExtent = count * pitch - Math.Max(0, Spacing);
      return vertical ? new Size(crossMax, mainExtent) : new Size(mainExtent, crossMax);
   }

   protected override void ArrangeVirtualized(Size finalSize, Vector2 offset)
   {
      var vertical = Orientation == Orientation.Vertical;
      var cross = vertical ? finalSize.Width : finalSize.Height;

      // Non-virtualized host: arrange each item at its cumulative start (from measure) with its OWN main extent.
      if (!IsVirtualizing)
      {
         var count = Owner.Items.Count;
         for (var i = 0; i < count && i < _itemStarts.Count; i++)
         {
            if (Owner.ItemContainerGenerator.ContainerFromIndex(i) is not IMeasurableComponent container) continue;
            var start = _itemStarts[i];
            var mainSize = vertical ? container.DesiredSize.Height : container.DesiredSize.Width;
            container.Arrange(vertical ? new Rect(0, start, cross, mainSize) : new Rect(start, 0, mainSize, cross));
         }
         return;
      }

      // ABSOLUTE slots (no -offset): the scroll offset is applied once, by the ScrollContentPresenter translating this
      // viewport-sized panel and clipping (same physical-scroll seam as WrapPanel). A tile's rect is constant across
      // scroll, so Arrange short-circuits for tiles that kept their index; only the rebound row re-runs ArrangeCore.
      // Snapshot indices into a REUSED buffer (not a fresh ToList): the window scan runs every scroll frame.
      _arrangeIndexBuf.Clear();
      _arrangeIndexBuf.AddRange(Owner.ItemContainerGenerator.RealizedIndices);

      // SLOT, not index: while a drop gap is open everything from it on moves along by one, opening one item-sized hole.
      var pitch = Pitch;
      Rect SlotRect(int slot) => vertical
         ? new Rect(0, slot * pitch, cross, _itemExtent)
         : new Rect(slot * pitch, 0, _itemExtent, cross);

      void ArrangeAt(int index)
      {
         if (Owner.ItemContainerGenerator.ContainerFromIndex(index) is not IMeasurableComponent container) return;
         container.Arrange(SlotRect(SlotOf(index)));
      }

      // Each tile's slot is CONSTANT from its index (absolute stack, no cumulative dependency), so the tiles' Arrange are
      // INDEPENDENT - fan them across cores when there are enough to amortise the thread overhead (same pattern + safety
      // as WrapPanel: the only shared write a tile arrange makes is RenderDirty.MarkGeometry, which is locked). Small
      // windows stay sequential.
      if (_arrangeIndexBuf.Count >= ParallelArrangeThreshold)
         System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, _arrangeIndexBuf.Count),
            range => { for (var i = range.Item1; i < range.Item2; i++) ArrangeAt(_arrangeIndexBuf[i]); });
      else
         foreach (var index in _arrangeIndexBuf) ArrangeAt(index);

      AnimateLayoutMoves(SlotRect);
      ReconcileDropPlaceholder(SlotRect);
   }

   // A uniform stack derives every slot from an index, exactly as the grid does, so the hole is one multiplication.
   public override bool SupportsDropGap => IsVirtualizing;

   /// <summary>Which slot a drop lands in, from the STACK's own arithmetic rather than from where the containers
   /// currently sit - the gap moves those, and an index read off them would chase itself. See VirtualizingPanel.</summary>
   public override bool TryGetDropSlot(Vector2 point, out int index)
   {
      index = -1;
      if (!IsItemsHost || !IsVirtualizing || _itemExtent <= 1) return false;

      var main = Math.Max(0, Orientation == Orientation.Vertical ? point.Y : point.X);
      index = Math.Clamp((int)(main / Pitch), 0, Owner.Items?.Count ?? 0);
      return true;
   }
}
