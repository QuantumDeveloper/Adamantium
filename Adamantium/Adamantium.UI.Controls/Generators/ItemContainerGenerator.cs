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
    private readonly Stack<ContentPresenter> _recyclePool = new();

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
        var container = PrepareContainer(item);
        _byIndex[index] = container;
        _indexByContainer[container] = index;
        return container;
    }

    private IUIComponent PrepareContainer(object item)
    {
        // The item already is its own container (e.g. a Button authored directly): host it as-is.
        if (item is IUIComponent ui) return ui;

        var presenter = _recyclePool.Count > 0 ? _recyclePool.Pop() : new ContentPresenter();
        presenter.DataContext = item;          // item template's {Binding}s resolve against the item
        presenter.ContentTemplate = _owner.ItemTemplate;
        presenter.Content = item;
        return presenter;
    }

    /// <summary>Releases the container at <paramref name="index"/> back to the pool (generated presenters) so it can be reused.</summary>
    public void Recycle(int index)
    {
        if (!_byIndex.Remove(index, out var container)) return;
        _indexByContainer.Remove(container);
        if (container is ContentPresenter presenter)
        {
            presenter.Content = null;
            presenter.DataContext = null;
            _recyclePool.Push(presenter);
        }
    }

    /// <summary>Drops every realized container and the recycle pool (e.g. on a Reset / ItemTemplate change).</summary>
    public void Clear()
    {
        _byIndex.Clear();
        _indexByContainer.Clear();
        _recyclePool.Clear();
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
