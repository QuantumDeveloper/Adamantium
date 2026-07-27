using System;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.UI.Core.Input;

/// <summary>In-app <see cref="IDataPackage"/>: a dictionary of format -> value. A bare CLR object is stored under its
/// type's full name; the OLE bridge (level 3) adds named standard formats over the same interface. A format may also be
/// a PROMISE (<see cref="SetDeferred"/>) that is only redeemed if someone reads it.</summary>
public sealed class DataPackage : IDataPackage
{
    private readonly Dictionary<string, object> _byFormat = new();

    public DataPackage()
    {
    }

    public DataPackage(object data)
    {
        Set(data);
    }

    public void Set(object data)
    {
        if (data != null)
        {
            _byFormat[data.GetType().FullName!] = data;
        }
    }

    public void Set(string format, object data) => _byFormat[format] = data;

    public void SetDeferred(string format, Func<object> produce)
    {
        ArgumentNullException.ThrowIfNull(produce);
        _byFormat[format] = new Promise(produce);
    }

    public bool IsDeferred(string format) => _byFormat.TryGetValue(format, out var v) && v is Promise { Redeemed: false };

    public object Get(string format) => _byFormat.TryGetValue(format, out var v) ? Value(v) : null;

    /// <summary>The first stored value assignable to <typeparamref name="T"/>. A promise is NOT redeemed here: this is
    /// the live-object question ("is this drag carrying a MyItem?"), which targeting code asks on every move - answering
    /// it must never run a producer. Address a deferred format by NAME.</summary>
    public T Get<T>()
    {
        foreach (var v in _byFormat.Values)
        {
            if (v is T t) return t;
        }
        return default;
    }

    public bool Contains(string format) => _byFormat.ContainsKey(format);

    /// <summary>Whether any MATERIALIZED value is a <typeparamref name="T"/> - see <see cref="Get{T}"/> for why a promise
    /// is not redeemed to answer this.</summary>
    public bool Contains<T>() => _byFormat.Values.Any(v => v is T);

    public IReadOnlyList<string> GetFormats() => _byFormat.Keys.ToList();

    private static object Value(object stored) => stored is Promise promise ? promise.Value : stored;

    /// <summary>A format that has been advertised but not produced. Redeemed at most ONCE - a target may ask for the
    /// same format several times during one drop, and rendering a heavy payload twice is exactly the cost deferring was
    /// meant to avoid.</summary>
    private sealed class Promise
    {
        private readonly Func<object> _produce;
        private object _value;

        public Promise(Func<object> produce) => _produce = produce;

        public bool Redeemed { get; private set; }

        public object Value
        {
            get
            {
                if (Redeemed) return _value;
                _value = _produce();
                Redeemed = true;
                return _value;
            }
        }
    }
}
