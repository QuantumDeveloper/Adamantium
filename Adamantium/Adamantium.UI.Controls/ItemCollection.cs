using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Adamantium.UI.Controls;

/// <summary>
/// The effective item list of an <see cref="ItemsControl"/>: either items authored directly in markup
/// (<c>&lt;ItemsControl&gt;&lt;Button/&gt;…</c>) or a view over an assigned <c>ItemsSource</c>. While a source is set the
/// collection is read-only (mutate the source instead), mirroring WPF. Raises <see cref="INotifyCollectionChanged"/>
/// so the control regenerates containers. Live subscription to a source's own change notifications is wired in Phase 2;
/// for now setting a source materialises a snapshot and raises Reset.
/// </summary>
public sealed class ItemCollection : IList<object>, IReadOnlyList<object>, INotifyCollectionChanged
{
    private readonly List<object> _direct = [];
    private IEnumerable _source;
    private List<object> _sourceSnapshot;

    public event NotifyCollectionChangedEventHandler CollectionChanged;

    internal bool HasSource => _sourceSnapshot != null;

    private IList<object> Active => _sourceSnapshot ?? (IList<object>)_direct;

    public int Count => Active.Count;

    public bool IsReadOnly => HasSource;

    public object this[int index]
    {
        get => Active[index];
        set
        {
            ThrowIfSourced();
            var old = _direct[index];
            _direct[index] = value;
            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, old, index));
        }
    }

    /// <summary>Switches the collection to view <paramref name="source"/> (null clears it, returning to direct items).
    /// If the source raises <see cref="INotifyCollectionChanged"/> its changes are mirrored live and forwarded.</summary>
    internal void SetSource(IEnumerable source)
    {
        if (_source is INotifyCollectionChanged oldObservable)
            oldObservable.CollectionChanged -= OnSourceCollectionChanged;

        _source = source;
        _sourceSnapshot = source?.Cast<object>().ToList();

        if (source is INotifyCollectionChanged observable)
            observable.CollectionChanged += OnSourceCollectionChanged;

        Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    // Mirror the source's change into the snapshot (so indexing stays correct), then forward the SAME args so the
    // ItemsControl can update containers incrementally with the source's own indices.
    private void OnSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                for (var i = 0; i < e.NewItems.Count; i++)
                    _sourceSnapshot.Insert(e.NewStartingIndex + i, e.NewItems[i]);
                break;
            case NotifyCollectionChangedAction.Remove:
                _sourceSnapshot.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                break;
            case NotifyCollectionChangedAction.Replace:
                for (var i = 0; i < e.NewItems.Count; i++)
                    _sourceSnapshot[e.OldStartingIndex + i] = e.NewItems[i];
                break;
            case NotifyCollectionChangedAction.Move:
            case NotifyCollectionChangedAction.Reset:
                _sourceSnapshot = _source?.Cast<object>().ToList() ?? [];
                Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                return;
        }

        Raise(e);
    }

    public void Add(object item)
    {
        ThrowIfSourced();
        _direct.Add(item);
        Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, _direct.Count - 1));
    }

    public void Insert(int index, object item)
    {
        ThrowIfSourced();
        _direct.Insert(index, item);
        Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
    }

    public bool Remove(object item)
    {
        ThrowIfSourced();
        var index = _direct.IndexOf(item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        ThrowIfSourced();
        var old = _direct[index];
        _direct.RemoveAt(index);
        Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, old, index));
    }

    public void Clear()
    {
        ThrowIfSourced();
        _direct.Clear();
        Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public bool Contains(object item) => Active.Contains(item);

    public int IndexOf(object item) => Active.IndexOf(item);

    public void CopyTo(object[] array, int arrayIndex) => Active.CopyTo(array, arrayIndex);

    public IEnumerator<object> GetEnumerator() => Active.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Raise(NotifyCollectionChangedEventArgs e) => CollectionChanged?.Invoke(this, e);

    private void ThrowIfSourced()
    {
        if (HasSource)
            throw new InvalidOperationException("Items is read-only while ItemsSource is set; modify the source collection instead.");
    }
}
