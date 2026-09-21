using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Adamantium.UI.Core.Data;

/// <summary>
/// Fans a shared <see cref="INotifyPropertyChanged"/> source out to its bound expressions through ONE subscription per
/// source, instead of one multicast-delegate subscription per binding. A virtualized list where thousands of tiles bind
/// the same sub-view-model (e.g. every tile's 12 <c>Stroke.*</c> bindings target one shared Pen) otherwise puts tens of
/// thousands of handlers on that source's <c>PropertyChanged</c>: firing is O(handlers) - unavoidable, the live ones
/// must update - but a raw <c>+=</c>/<c>-=</c> rebuilds the whole multicast invocation list (O(handlers)), so releasing a
/// shrunk realized window is O(N^2) and the binding engine is forced to KEEP dead subscriptions rather than pay it. Here
/// add/remove is O(1) (a weak set), so a container can unsubscribe the instant it leaves the window, and a fire only ever
/// reaches LIVE subscribers. Weak subscriber refs -> a target GC'd without an explicit teardown is pruned automatically.
/// </summary>
internal static class SharedSourceRegistry
{
    // source (WEAK key -> the registry never pins a source) -> its single fan-out entry.
    private static readonly ConditionalWeakTable<INotifyPropertyChanged, SourceEntry> _bySource = new();

    public static void Subscribe(INotifyPropertyChanged source, BindingExpression binding)
        => _bySource.GetValue(source, static s => new SourceEntry(s)).Add(binding);

    public static void Unsubscribe(INotifyPropertyChanged source, BindingExpression binding)
    {
        if (_bySource.TryGetValue(source, out var entry)) entry.Remove(binding);
    }

    private sealed class SourceEntry
    {
        private static readonly object Present = new();
        // Weak SET of subscribers: O(1) add/remove, and a subscriber whose target was GC'd is dropped automatically.
        private readonly ConditionalWeakTable<BindingExpression, object> _subscribers = new();
        private readonly object _gate = new();
        private BindingExpression[] _fireBuf = new BindingExpression[16];   // reused fire snapshot (no per-fire alloc)

        public SourceEntry(INotifyPropertyChanged source) => source.PropertyChanged += OnSourceChanged;

        public void Add(BindingExpression b) { lock (_gate) _subscribers.AddOrUpdate(b, Present); }
        public void Remove(BindingExpression b) { lock (_gate) _subscribers.Remove(b); }

        // The source's single PropertyChanged handler; fan out to each live subscriber. Snapshot under the gate (so a
        // cross-thread subscribe/unsubscribe can't mutate the weak set mid-enumeration), then call out OUTSIDE the lock -
        // a producer binding (MultiBinding child) applies synchronously and could re-enter Subscribe/Unsubscribe, which
        // must not deadlock or corrupt the enumeration. One fire per source at a time (a single object's property
        // mutations are serialized), so the reused buffer is race-free.
        private void OnSourceChanged(object sender, PropertyChangedEventArgs e)
        {
            int n = 0;
            lock (_gate)
            {
                foreach (var kv in _subscribers)
                {
                    if (n == _fireBuf.Length) Array.Resize(ref _fireBuf, n * 2);
                    _fireBuf[n++] = kv.Key;
                }
            }
            for (var i = 0; i < n; i++)
            {
                _fireBuf[i].OnSourcePropertyChanged(sender, e);
                _fireBuf[i] = null;   // don't pin subscribers between fires
            }
        }
    }
}
