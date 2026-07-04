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

    public ItemContainerGenerator(ItemsControl owner)
    {
        _owner = owner;
    }

    /// <summary>The indices currently realized (for a virtualizing panel: only the visible window).</summary>
    public IReadOnlyCollection<int> RealizedIndices => _byIndex.Keys;

    /// <summary>How many containers are currently realized.</summary>
    public int RealizedCount => _byIndex.Count;

    public IUIComponent ContainerFromIndex(int index) => _byIndex.GetValueOrDefault(index);

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
    public IReadOnlyList<IUIComponent> SetWindow(int first, int last)
    {
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

        // 2. Give every in-window index that lacks a container a donor (rebound in place) or a fresh one.
        var next = 0;
        for (var i = first; i <= last; i++)
        {
            if (_byIndex.ContainsKey(i)) continue;   // stayed in the window - keep its container + binding
            var item = _owner.Items[i];
            IUIComponent container;
            if (_owner.IsItemItsOwnContainer(item))
            {
                container = (IUIComponent)item;
            }
            else if (next < donors.Count)
            {
                container = donors[next++];
                _owner.PrepareContainer(container, item);   // rebind: DataContext/content -> the new item
            }
            else
            {
                container = _owner.GetContainerForItem();
                _generated.Add(container);
                _owner.PrepareContainer(container, item);
            }
            _byIndex[i] = container;
            _indexByContainer[container] = i;
            // In-window => visible: a recycled donor parked as surplus was Collapsed and now holds a new item, so re-show
            // it (keeps the panel's IsMeasureValid-only skip correct + stops a rebound tile silently going missing). Guard
            // the set: an out-of-window donor reused in place is ALREADY Visible, and Visibility set would otherwise
            // re-propagate down its whole subtree every rebind on the scroll hot path.
            if (container.Visibility != Visibility.Visible) container.Visibility = Visibility.Visible;
        }

        // 3. Donors not reused (the window shrank) - park them for when it grows again, and report them so the panel
        //    hides ONLY these. During steady scroll donors == orphans, so this is empty and nothing is ever collapsed.
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
        // The item already is its own container (e.g. a Button authored directly): host it as-is, never recycle it.
        if (_owner.IsItemItsOwnContainer(item)) return (IUIComponent)item;

        var container = _recyclePool.Count > 0 ? _recyclePool.Pop() : _owner.GetContainerForItem();
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
