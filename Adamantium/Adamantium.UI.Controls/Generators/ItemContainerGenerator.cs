using System.Collections.Generic;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Generators;

/// <summary>
/// Maps an <see cref="ItemsControl"/>'s items to their UI containers, by index (no WPF-style cursor/GeneratorPosition).
/// A panel asks <see cref="Realize"/>/<see cref="Recycle"/> for the indices it needs; a virtualizing panel realizes only
/// the visible window. An item that already is a UI component is its own container; otherwise a recycled (or new)
/// <see cref="ContentPresenter"/> projects it via the control's ItemTemplate. Contract: the container is a pure
/// projection of the item — state that must survive recycling lives on the item/view-model, never on the container.
/// </summary>
public class ItemContainerGenerator
{
    private readonly ItemsControl _owner;
    private readonly Dictionary<int, IUIComponent> _byIndex = new();
    private readonly Dictionary<IUIComponent, int> _indexByContainer = new();
    private readonly Stack<IUIComponent> _recyclePool = new();
    private readonly HashSet<IUIComponent> _generated = new();   // containers we created (recyclable); item-is-own are not
    private readonly List<IUIComponent> _donorBuf = new();       // reused across SetWindow calls - zero per-scroll-frame alloc
    private readonly List<int> _outKeysBuf = new();
    private readonly List<IUIComponent> _surplusBuf = new();
    private readonly List<int> _pendingBuf = new();              // in-window slots deferred this pass (budget spent) - skeletons
    private int _lastWindowFirst;                                // previous window start - fallback direction on a far jump
    private readonly List<int> _bindOrderBuf = new();            // missing slots in nearest-to-realized-first bind order
    private List<int> _waveBuf = new();                          // current expansion layer (swapped with next - not readonly)
    private List<int> _nextWaveBuf = new();
    private bool[] _reachedBuf = [];                             // per-window-slot visited marks for the expansion

    public ItemContainerGenerator(ItemsControl owner)
    {
        _owner = owner;
    }

    /// <summary>The indices currently realized (for a virtualizing panel: only the visible window).</summary>
    public IReadOnlyCollection<int> RealizedIndices => _byIndex.Keys;

    /// <summary>In-window slots the last <see cref="SetWindow"/> DEFERRED because the per-frame rebind budget was spent.
    /// They have no real container yet (the panel shows a skeleton there); the next pass rebinds them. Empty when the
    /// whole window fit the budget (normal/slow scroll).</summary>
    public IReadOnlyList<int> PendingIndices => _pendingBuf;

    /// <summary>How many containers are currently realized.</summary>
    public int RealizedCount => _byIndex.Count;

    public IUIComponent ContainerFromIndex(int index) => _byIndex.GetValueOrDefault(index);

    /// <summary>Any currently-realized container, or null if none. Lets the panel sample a real item (e.g. to read the
    /// tile margin a skeleton card must mirror) without knowing which indices are realized.</summary>
    public IUIComponent AnyRealizedContainer()
    {
        foreach (var container in _byIndex.Values) return container;
        return null;
    }

    public int IndexFromContainer(IUIComponent container) =>
        container != null && _indexByContainer.TryGetValue(container, out var i) ? i : -1;

    /// <summary>Returns the (cached) container for the item at <paramref name="index"/>, creating/recycling one if needed.</summary>
    public IUIComponent Realize(int index)
    {
        if (_byIndex.TryGetValue(index, out var existing)) return existing;

        var item = _owner.Items[index];
        var container = ProduceContainer(item);
        _byIndex[index] = container;
        _indexByContainer[container] = index;
        return container;
    }

    /// <summary>
    /// Reconciles the realized set to EXACTLY the indices [<paramref name="first"/>, <paramref name="last"/>] for a
    /// virtualizing panel. Containers whose index left the window are REBOUND in place to indices that entered (the
    /// fixed working set of a static viewport): no Visibility churn, no DataContext clearing, and - crucially - each
    /// container ends up under exactly ONE index (so a container can never be drawn for two slots). Idempotent: calling
    /// it again with the same range is a no-op. Returns the surplus containers (only when the window is now smaller than
    /// before - i.e. at a list edge where fewer items exist than the window) so the panel can hide just those.
    /// </summary>
    public IReadOnlyList<IUIComponent> SetWindow(int first, int last, double budgetMs = double.MaxValue, int minBinds = 0,
        Action<IUIComponent> onBound = null)
    {
        var budgetStart = System.Diagnostics.Stopwatch.GetTimestamp();
        // 1. Unmap every realized index now outside the window; its generated container becomes a reusable donor (kept
        //    fully intact - same visual, same DataContext - until step 2 rebinds it). Item-is-own-container isn't reused.
        // Reused buffers + a plain loop (no LINQ closure/ToList): SetWindow runs every scroll frame, so the old per-call
        // Where().ToList() + two fresh Lists were the GC spikes (25-57 ms pauses) seen under scroll.
        var donors = _donorBuf;
        donors.Clear();
        _outKeysBuf.Clear();
        foreach (var idx in _byIndex.Keys)
            if (idx < first || idx > last) _outKeysBuf.Add(idx);   // snapshot: can't remove from _byIndex mid-enumeration
        foreach (var idx in _outKeysBuf)
        {
            var container = _byIndex[idx];
            _byIndex.Remove(idx);
            _indexByContainer.Remove(container);
            if (_generated.Contains(container)) donors.Add(container);
        }
        // Also draw on containers parked by a previous shrink (the window growing back).
        while (_recyclePool.Count > 0) donors.Add(_recyclePool.Pop());

        // 2. Give every in-window index that lacks a container a donor (rebound in place) or a fresh one. A rebind (new
        //    DataContext -> re-resolve the item template's bindings + re-measure) is the real per-tile cost, so at most
        //    maxRebinds of them run this pass: an aggressive fling that turns the whole window over in one frame would
        //    otherwise do O(window) rebinds/frame. Slots past the budget are DEFERRED (collected in _pendingBuf); the
        //    panel shows a skeleton there and the next pass rebinds them (unused donors were pooled in step 3, so they're
        //    drained back as donors next time). A normal/slow scroll rebinds far fewer than the budget, so nothing defers.
        _pendingBuf.Clear();
        var next = 0;
        var rebinds = 0;
        // Bind missing slots NEAREST-TO-REALIZED-FIRST: grow the realized region contiguously OUTWARD from the content
        // already on screen, whichever side the window extends. A plain first->last loop opened a second island at the
        // window's far edge when scrolling UP (kept block at the bottom, binding started at the top) with a skeleton band
        // between them - and a scroll-DIRECTION heuristic still mis-ordered the CONTINUATION passes that drain the
        // budget-deferred backlog with a STATIC window (delta=0 read as "forward" -> top-down again after an up-scroll).
        // Nearest-first is direction-agnostic: a layered expansion from every realized slot orders the missing ones by
        // distance to the kept content in O(window), so the budget always defers the FAR edge - where a skeleton band
        // looks natural - never a mid-viewport strip. A far jump (nothing kept in-window) falls back to scroll direction.
        _bindOrderBuf.Clear();
        var winSize = last - first + 1;
        if (winSize > 0)
        {
            if (_reachedBuf.Length < winSize) _reachedBuf = new bool[Math.Max(winSize, _reachedBuf.Length * 2)];
            Array.Clear(_reachedBuf, 0, winSize);
            _waveBuf.Clear();
            foreach (var idx in _byIndex.Keys)
                if (idx >= first && idx <= last) { _reachedBuf[idx - first] = true; _waveBuf.Add(idx); }
            if (_waveBuf.Count == 0)
            {
                var reverse = first < _lastWindowFirst;   // far jump: realize from the edge the content enters
                for (var k = 0; k < winSize; k++) _bindOrderBuf.Add(reverse ? last - k : first + k);
            }
            else
            {
                while (_waveBuf.Count > 0)
                {
                    _nextWaveBuf.Clear();
                    foreach (var idx in _waveBuf)
                    {
                        var up = idx - 1;
                        var down = idx + 1;
                        if (up >= first && !_reachedBuf[up - first]) { _reachedBuf[up - first] = true; _bindOrderBuf.Add(up); _nextWaveBuf.Add(up); }
                        if (down <= last && !_reachedBuf[down - first]) { _reachedBuf[down - first] = true; _bindOrderBuf.Add(down); _nextWaveBuf.Add(down); }
                    }
                    (_waveBuf, _nextWaveBuf) = (_nextWaveBuf, _waveBuf);
                }
            }
        }
        _lastWindowFirst = first;
        foreach (var i in _bindOrderBuf)
        {
            if (_byIndex.ContainsKey(i)) continue;   // stayed in the window - keep its container + binding
            var item = _owner.Items[i];
            IUIComponent container;
            if (_owner.IsItemItsOwnContainer(item))
            {
                container = (IUIComponent)item;
                _owner.PrepareContainer(container, item);
            }
            else if (rebinds >= minBinds &&
                     System.Diagnostics.Stopwatch.GetElapsedTime(budgetStart).TotalMilliseconds >= budgetMs)
            {
                // TIME budget spent (checked AFTER a minimum of minBinds so we always make progress): defer this slot -
                // skeleton this frame, real (re)bind next pass. Time-based, not count-based, so an EXPENSIVE burst
                // (creating containers from empty, or after a DPI/resize regime change) stops after budgetMs instead of
                // running a whole stale count and freezing the frame; a CHEAP burst (scroll rebinds) fits many in the
                // same budget.
                _pendingBuf.Add(i);
                continue;
            }
            else if (next < donors.Count)
            {
                container = donors[next++];
                rebinds++;
                _owner.PrepareContainer(container, item);   // rebind: DataContext/content -> the new item
            }
            else
            {
                container = _owner.GetContainerForItem(item);
                _generated.Add(container);
                rebinds++;
                _owner.PrepareContainer(container, item);
            }
            _byIndex[i] = container;
            _indexByContainer[container] = i;
            // In-window => visible: a recycled donor parked as surplus was Collapsed and now holds a new item, so re-show
            // it (keeps the panel's IsMeasureValid-only skip correct + stops a rebound tile silently going missing). Guard
            // the set: an out-of-window donor reused in place is ALREADY Visible, and Visibility set would otherwise
            // re-propagate down its whole subtree every rebind on the scroll hot path.
            if (container.Visibility != Visibility.Visible) container.Visibility = Visibility.Visible;
            // Attach + MEASURE this tile now, INSIDE the time budget (via the panel's callback): a rebind is cheap but the
            // measure that follows it is the expensive part, so measuring here (not in a separate unbudgeted loop) is what
            // makes the budget actually bound the frame - the next iteration's time check sees the measure cost too.
            onBound?.Invoke(container);
        }

        // 3. Donors not reused (the window shrank) - park them for when it grows again, and report them so the panel hides
        //    (and deactivates the bindings of) ONLY these. During steady scroll donors == orphans, so this is empty and
        //    nothing is ever parked. A parked container stays attached + pooled for cheap reuse; the panel's ParkContainer
        //    deactivates its bindings so it also leaves any shared source's fan-out (no storm on a shared-property change).
        if (next >= donors.Count) return System.Array.Empty<IUIComponent>();
        _surplusBuf.Clear();   // reused; the caller iterates it synchronously before the next SetWindow
        for (; next < donors.Count; next++)
        {
            _recyclePool.Push(donors[next]);
            _surplusBuf.Add(donors[next]);
        }
        return _surplusBuf;
    }

    private IUIComponent ProduceContainer(object item)
    {
        // The item already is its own container (e.g. a Button authored directly): host it as-is and never recycle it.
        // It is still PREPARED - a control hooking its containers (a selection to reflect, an event to subscribe) has
        // nowhere else to do it, and skipping the call left everything an author wrote in markup silently inert.
        if (_owner.IsItemItsOwnContainer(item))
        {
            var own = (IUIComponent)item;
            _owner.PrepareContainer(own, item);
            return own;
        }

        var container = _recyclePool.Count > 0 ? _recyclePool.Pop() : _owner.GetContainerForItem(item);
        _generated.Add(container);
        _owner.PrepareContainer(container, item);
        return container;
    }

    /// <summary>Pools a generated container that is attached but no longer mapped to any index (a scroll/recycle edge
    /// case left it visible). Returning it to the pool makes it reusable instead of leaking - the panel hides it and the
    /// next <see cref="SetWindow"/> draws it as a donor again.</summary>
    public void ReclaimDetached(IUIComponent container)
    {
        if (container == null) return;
        if (_indexByContainer.ContainsKey(container)) return;   // still realized -> keep
        if (!_generated.Contains(container)) return;            // item-is-own-container -> not ours to pool
        if (!_recyclePool.Contains(container)) _recyclePool.Push(container);
    }

    /// <summary>Releases the container at <paramref name="index"/> back to the pool (generated containers) so it can be reused.</summary>
    public void Recycle(int index)
    {
        if (!_byIndex.Remove(index, out var container)) return;
        _indexByContainer.Remove(container);
        if (_generated.Contains(container))
        {
            _owner.ClearContainer(container);
            _recyclePool.Push(container);
        }
    }

    /// <summary>Drops every realized container and the recycle pool (e.g. on a Reset / ItemTemplate change).</summary>
    public void Clear()
    {
        _byIndex.Clear();
        _indexByContainer.Clear();
        _recyclePool.Clear();
        _generated.Clear();
    }

    /// <summary>Shifts realized indices to account for <paramref name="count"/> items inserted at <paramref name="index"/>.</summary>
    public void OnItemsInserted(int index, int count) => Reindex(k => k >= index ? k + count : k);

    /// <summary>Shifts realized indices after <paramref name="count"/> items were removed at <paramref name="index"/>
    /// (recycle the removed indices first).</summary>
    public void OnItemsRemoved(int index, int count) => Reindex(k => k >= index + count ? k - count : k);

    private void Reindex(System.Func<int, int> map)
    {
        var snapshot = new List<KeyValuePair<int, IUIComponent>>(_byIndex);
        _byIndex.Clear();
        _indexByContainer.Clear();
        foreach (var pair in snapshot)
        {
            var newIndex = map(pair.Key);
            _byIndex[newIndex] = pair.Value;
            _indexByContainer[pair.Value] = newIndex;
        }
    }
}
