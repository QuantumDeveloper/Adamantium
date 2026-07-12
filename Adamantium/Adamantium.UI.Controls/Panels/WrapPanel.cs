using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Panels;

public class WrapPanel : VirtualizingPanel, IHitTestChildren
{
   private readonly IUIComponent[] _hitOne = new IUIComponent[1];   // reused single-child hit-test result (no per-move alloc)

   // O(1) hit-test: the tile that a point can hit is the ONE at its grid slot (tiles are absolute + non-overlapping), so
   // resolve the slot by arithmetic and return just that container/skeleton - instead of the base walk visiting every
   // realized tile's whole subtree (thousands of nodes) on every mouse move (the second-monitor freeze). `local` is in
   // panel space (absolute slot coordinates); null = let the caller do the default full walk (a plain, non-items panel).
   IReadOnlyList<IUIComponent> IHitTestChildren.GetHitTestChildren(Vector2 local)
   {
      if (!IsItemsHost) return null;
      var horizontal = Orientation == Orientation.Horizontal;
      var flow = horizontal ? local.X : local.Y;
      var scroll = horizontal ? local.Y : local.X;
      if (flow < 0 || scroll < 0 || _cellFlow <= 0 || _cellScroll <= 0) return System.Array.Empty<IUIComponent>();
      var col = (int)(flow / _cellFlow);
      if (col >= _columns) return System.Array.Empty<IUIComponent>();
      var index = (int)(scroll / _cellScroll) * _columns + col;
      var hit = Owner.ItemContainerGenerator.ContainerFromIndex(index);   // the loading overlay is hit-transparent
      if (hit == null) return System.Array.Empty<IUIComponent>();
      _hitOne[0] = hit;
      return _hitOne;
   }

   // ---- Virtualized 2D state (items host) ----
   private const int Buffer = 2;        // extra lead lines on each side of the viewport: a row must be realized BEFORE the
                                        // scroll reveals it. With 1 the lead was consumed by the reveal outpacing a
                                        // 1-frame realize (a row visibly "catching up" to the scroll, esp. scrolling UP
                                        // where the top lead is thinnest); 2 keeps a realized row ahead of the edge.
   private const int MaxCellPasses = 4; // bound the in-pass convergence of the auto-sized cell
   // Per-frame (re)bind TIME budget. A rebind is cheap (~20 us) but CREATING a container (new item + template + bindings)
   // is ~50x that, so a fixed COUNT that is fine for scroll would spend >100 ms/frame building the initial window (or a
   // post-resize burst) - freezing the app. Instead SetWindow (re)binds until BindBudgetMs of frame time is spent, then
   // defers the rest to skeletons + the next pass. Time-based self-tunes INSTANTLY to per-op cost: few EXPENSIVE
   // creates/frame (UI stays live, skeletons show progress) but many CHEAP rebinds/frame (fast scroll) - with no
   // count-estimate to mis-size on a cheap->expensive regime change (the multi-monitor DPI-resize freeze).
   private const double BindBudgetMs = 6.0;   // frame-time slice for (re)binds WHILE SCROLLING (headroom under a 16 ms frame)
   private const int ParallelArrangeThreshold = 64;   // arrange tiles across cores only above this many realized (else thread overhead > win)
   private const double FillBudgetMs = 30.0;  // slice when NOT scrolling (initial fill / a settled fling): drain the backlog fast
   private const int MinBinds = 8;            // always (re)bind at least this many/frame so the window keeps filling

   // The offset the previous measure ran against - tells an ACTIVE scroll (offset moving frame-to-frame) from a static fill.
   // A REALIZE budget must be a GUARANTEED slice, never "whatever the frame has left": the per-frame O(window) overhead
   // (skeleton reconcile + bindorder + render build) can eat a 12 ms frame ceiling BEFORE the bind loop runs, leaving it
   // ~0 -> only MinBinds/frame -> a huge cold fill dribbles ~10 tiles/frame for tens of seconds, paying the O(window)
   // overhead hundreds of times. A fixed generous fill slice blasts the window in a handful of frames instead (the total
   // bind work is fixed; a big slice just pays the O(window) overhead a dozen times, not ~400).
   private Vector2 _lastMeasuredOffset = new(double.NaN, double.NaN);

   // The last FINITE viewport extent on the scroll axis. When a measure comes in with an infinite scroll-axis viewport (a
   // ScrollViewer probing its content's extent, e.g. the first measure on tab-entry), we realize a window sized to this
   // instead of the whole list - so a tab-entry doesn't build all N tiles. Seeded to a default screenful until a real one.
   private double _lastViewportScroll;
   private const double DefaultViewportScroll = 1080.0;   // fallback viewport (px) before any real one is known

   private bool _measuringHorizontal;               // orientation snapshot for OnSlotBound (called from SetWindow)
   private bool _cellGrew;                           // did a bound tile grow the auto cell? -> another MaxCellPasses pass
   private System.Action<IUIComponent> _onSlotBound; // cached delegate (no per-frame closure alloc)
   // Called by SetWindow for each newly-(re)bound tile, INSIDE the time budget: attach (fresh ones), measure, and grow
   // the auto cell to fit. Measuring HERE (not in a later loop) is what lets the budget bound bind+measure together.
   private void OnSlotBound(IUIComponent container)
   {
      if (container.VisualParent != this) { AddVisualChild(container); AddLogicalChild(container); }
      var m = (IMeasurableComponent)container;
      if (!m.IsMeasureValid) m.Measure(CellConstraint(_measuringHorizontal));
      _cellGrew |= GrowCell(_measuringHorizontal, m.DesiredSize);
   }
   private double _cellFlow = 1;        // cell size along the flow axis
   private double _cellScroll = 1;      // cell size along the scroll (wrap) axis
   private int _columns = 1;            // items per line
   private int _lastFirstLine;          // remembered first visible line -> probe next pass
   private int _lastItemCount = -1;     // detect data changes -> re-establish the cached cell

   /// <summary>Grid metrics from the last virtualized measure (items-host mode): items per line, and the uniform cell
   /// PITCH along the flow / scroll axes (cell size incl. the inter-cell gap). An items host built on this panel reads
   /// them to place things by ABSOLUTE index rather than realized bounds - e.g. TilesHost computes each tile's shared-photo
   /// UV slice from its item index + these, so the photo maps correctly with virtualization on. Valid after a measure.</summary>
   public int Columns => _columns;
   public double CellFlow => _cellFlow;
   public double CellScroll => _cellScroll;

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

   // The realized window is a band of ROWS starting at firstLine = max(0, floor(scrollOffset / cellScroll) - Buffer).
   // It shifts only when that top line changes - i.e. when the scroll offset crosses a whole-row boundary. A sub-pixel
   // scroll that stays within the current row leaves the window (and every realized container) exactly as it was, so the
   // base can skip re-realizing it and just slide + track the thumb. Cell not measured yet (_cellScroll <= 0) -> re-realize.
   protected override bool RealizedWindowMovesFor(Vector2 from, Vector2 to)
   {
      if (_cellScroll <= 0) return true;
      var horizontal = Orientation == Orientation.Horizontal;
      var fromScroll = horizontal ? from.Y : from.X;
      var toScroll = horizontal ? to.Y : to.X;
      var fromLine = Math.Max(0, (int)Math.Floor(fromScroll / _cellScroll) - Buffer);
      var toLine = Math.Max(0, (int)Math.Floor(toScroll / _cellScroll) - Buffer);
      return fromLine != toLine;
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

      // Guaranteed (re)bind slice: small while actively scrolling (stay responsive), large on a static fill (drain the
      // backlog in a handful of frames). NOT the frame's leftover time - see _lastMeasuredOffset's note on why leftovers
      // starve the fill to ~10 tiles/frame.
      var scrolling = offset != _lastMeasuredOffset;
      _lastMeasuredOffset = offset;
      var bindBudget = scrolling ? BindBudgetMs : FillBudgetMs;

      // Resolve columns + the realized window and measure it; if a realized item is bigger than the assumed uniform
      // cell, grow the cell (only along axes the user didn't pin) and resolve again. This converges in a couple of passes
      // and then stays put across scrolls. The old code re-probed ONE variable-width item every pass, so the cell and
      // column count flickered -> the whole grid reflowed (overlapping/garbled rows) and every frame re-bound the window.
      int first = 0, last = -1;
      for (var pass = 0; pass < MaxCellPasses; pass++)
      {
         _columns = Math.Max(1, (int)Math.Floor(viewportFlow / _cellFlow));
         var lines = (count + _columns - 1) / _columns;

         // A ScrollViewer measures its content UNCONSTRAINED on the scroll axis (to learn the natural extent), so a
         // virtualizing panel gets viewportScroll == infinity on the FIRST measure after (re)entering a view - BEFORE
         // arrange establishes the real viewport. Realizing all `count` items then (the old OnNoViewport path) rebuilt the
         // WHOLE list every tab-entry (the 4590-tile freeze). Instead realize a BOUNDED window sized to the last real
         // viewport (or a default screenful), and still return the full extent below - so the ScrollViewer gets correct
         // scrollbars and the next measure, with the real finite viewport, corrects the window. O(count) freeze -> O(viewport).
         double effectiveViewport;
         if (double.IsInfinity(viewportScroll))
         {
            OnNoViewport();
            effectiveViewport = _lastViewportScroll > 0 ? _lastViewportScroll : DefaultViewportScroll;
         }
         else
         {
            effectiveViewport = viewportScroll;
            _lastViewportScroll = viewportScroll;
         }

         var scrollOffset = horizontal ? offset.Y : offset.X;
         // Recycling-ring invariant #1 (CONSTANT window): derive BOTH edges from the same floor(top) so they move in
         // lockstep. Independent floor(top)/ceil(bottom) advance at different sub-cell offsets, so a fractional (real
         // inertia) scroll oscillated the window by one row each frame - which broke donor reuse and churned a whole
         // row's Visibility every frame. A fixed spanRows slides cleanly: floor advances 1 => first++ AND last++, so
         // every leaving row's container is reused for the entering row. spanRows covers viewport + top/bottom buffer.
         var spanRows = (int)Math.Ceiling(effectiveViewport / _cellScroll) + 1 + 2 * Buffer;
         var topLine = (int)Math.Floor(scrollOffset / _cellScroll);
         var firstLine = Math.Max(0, topLine - Buffer);
         var lastLine = Math.Min(lines - 1, firstLine + spanRows);
         _lastFirstLine = firstLine;
         first = firstLine * _columns;
         last = Math.Min(count - 1, (lastLine + 1) * _columns - 1);
         // The window is ALWAYS the full visible range - NOT truncated to a per-frame realize cap. A big burst (a huge
         // viewport filling from empty, or a far fling) would otherwise hang one frame building ~hundreds of containers
         // (the resize freeze); instead SetWindow's RebindBudget caps how many are (re)bound this pass and the rest
         // become PendingIndices - covered by skeletons this frame and streamed in over the next passes. So the whole
         // viewport shows content (real or skeleton) immediately, and the fill is bounded WITHOUT shrinking the window.

         // Reconcile the realized grid window to exactly [first,last] (rebind in place; hide only true surplus). Cap the
         // rebinds this pass to RebindBudget: an aggressive fling that turns the whole window over in one frame does
         // O(window) rebinds (layout+render both scale with it) and drops frames. Past the budget, slots are DEFERRED
         // (generator.PendingIndices) - a skeleton fills them this frame and the next pass rebinds them. Slow/normal
         // scroll rebinds far fewer than the budget, so nothing defers and there is zero visible difference.
         // Bind + attach + MEASURE each newly-(re)bound tile INSIDE SetWindow's time budget (OnSlotBound does the measure),
         // so the budget bounds bind+measure together - the expensive measure is no longer a separate unbudgeted loop that
         // blows the frame (the multi-monitor/resize 2200-tile freeze). Deferred slots become PendingIndices -> skeletons.
         // GrowCell (unpinned-cell convergence) feeds back through _cellGrew.
         _measuringHorizontal = horizontal;
         _cellGrew = false;
         _onSlotBound ??= OnSlotBound;

         foreach (var c in Owner.ItemContainerGenerator.SetWindow(first, last, bindBudget, MinBinds, _onSlotBound))
            c.Visibility = Visibility.Collapsed;
         if (!_cellGrew) break;
      }

      // Budget-deferred slots remain this frame -> continue on the NEXT pass to (re)bind the next RebindBudget slice
      // (skeletons cover them meanwhile). Must be the next-pass primitive, not a bare InvalidateMeasure: we are inside
      // the layout pass (with _inLayout set, which mutes the panel's own InvalidateMeasure anyway), and a same-pass
      // re-measure would just try to realize the whole window this frame - defeating the budget.
      if (Owner.ItemContainerGenerator.PendingIndices.Count > 0)
         LayoutManager.For(this).InvalidateMeasureNextPass(this);

      var totalLines = (count + _columns - 1) / _columns;
      var flowExtent = _columns * _cellFlow;
      var scrollExtent = totalLines * _cellScroll;
      return horizontal ? new Size(flowExtent, scrollExtent) : new Size(scrollExtent, flowExtent);
   }

   private readonly List<int> _arrangeIndexBuf = [];   // reused each arrange - no per-frame List alloc over the window

   protected override void ArrangeVirtualized(Size finalSize, Vector2 offset)
   {
      var horizontal = Orientation == Orientation.Horizontal;

      // ABSOLUTE grid slots - the scroll offset is applied by the ScrollContentPresenter translating this panel and
      // clipping (transform-only scroll), NOT baked into each tile. A tile's rect is then CONSTANT across scroll, so
      // Arrange short-circuits for tiles that kept their index; only the rebound row re-runs ArrangeCore (O(one row)).
      // Snapshot indices into a REUSED buffer (not a fresh ToList): the window scan runs every scroll frame, so the
      // per-frame list alloc over ~800 realized indices was steady gen0 churn.
      _arrangeIndexBuf.Clear();
      _arrangeIndexBuf.AddRange(Owner.ItemContainerGenerator.RealizedIndices);

      void ArrangeAt(int index)
      {
         if (Owner.ItemContainerGenerator.ContainerFromIndex(index) is not IMeasurableComponent container) return;
         var line = index / _columns;
         var col = index % _columns;
         var flowPos = col * _cellFlow;
         var scrollPos = line * _cellScroll;
         container.Arrange(horizontal
            ? new Rect(flowPos, scrollPos, _cellFlow, _cellScroll)
            : new Rect(scrollPos, flowPos, _cellScroll, _cellFlow));
      }

      // Each tile's slot is CONSTANT from its index (absolute grid, no cumulative dependency), so the tiles' Arrange are
      // INDEPENDENT - fan them across cores when there are enough to amortise the thread overhead (a maximize-to-4K storm
      // arranges thousands at once; a range Partitioner keeps per-tile overhead low). Small windows stay sequential.
      // The only shared write a tile arrange makes is RenderDirty.MarkGeometry (locked); diagnostic counters race harmlessly.
      if (_arrangeIndexBuf.Count >= ParallelArrangeThreshold)
         System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, _arrangeIndexBuf.Count),
            range => { for (var i = range.Item1; i < range.Item2; i++) ArrangeAt(_arrangeIndexBuf[i]); });
      else
         foreach (var index in _arrangeIndexBuf) ArrangeAt(index);

      // Budget-deferred slots (generator.PendingIndices): show a pooled per-slot loading skeleton card at each (a fast
      // fling / cold fill shows pulsing placeholders instead of holes). Reconciled here (after the real tiles) since this
      // is where the slot geometry lives; the panel owns the cards' lifecycle.
      ReconcileSkeletons(i =>
      {
         var line = i / _columns;
         var col = i % _columns;
         var flowPos = col * _cellFlow;
         var scrollPos = line * _cellScroll;
         return horizontal
            ? new Rect(flowPos, scrollPos, _cellFlow, _cellScroll)
            : new Rect(scrollPos, flowPos, _cellScroll, _cellFlow);
      });
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