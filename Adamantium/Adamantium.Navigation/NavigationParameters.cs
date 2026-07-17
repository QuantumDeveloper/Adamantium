using System;
using System.Collections;
using System.Collections.Generic;

namespace Adamantium.Navigation;

/// <summary>Typed parameter bag passed through a navigation (and a dialog). Holds strongly-typed values via
/// <see cref="Add"/> and reads them back with <see cref="GetValue{T}"/>/<see cref="TryGetValue{T}"/>; also parses a
/// query string (<c>"id=5&amp;mode=edit"</c>) as an escape hatch.</summary>
public sealed class NavigationParameters : IEnumerable<KeyValuePair<string, object>>
{
    private readonly Dictionary<string, object> _values = new();

    public NavigationParameters()
    {
    }

    public NavigationParameters(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0) _values[pair] = null;
            else _values[pair.Substring(0, eq)] = pair.Substring(eq + 1);
        }
    }

    public int Count => _values.Count;

    public object this[string key]
    {
        get => _values.TryGetValue(key, out var value) ? value : null;
        set => _values[key] = value;
    }

    public bool ContainsKey(string key) => _values.ContainsKey(key);

    /// <summary>Fluent add - returns this so calls can chain: <c>new NavigationParameters().Add("id", 5).Add("mode", "edit")</c>.</summary>
    public NavigationParameters Add(string key, object value)
    {
        _values[key] = value;
        return this;
    }

    public bool TryGetValue<T>(string key, out T value)
    {
        if (_values.TryGetValue(key, out var raw) && TryConvert(raw, out value)) return true;
        value = default;
        return false;
    }

    public T GetValue<T>(string key, T fallback = default) => TryGetValue<T>(key, out var value) ? value : fallback;

    // Direct hit first (the common typed-payload case); a string from the query ctor is coerced (id "5" -> int 5).
    private static bool TryConvert<T>(object raw, out T value)
    {
        if (raw is T typed) { value = typed; return true; }
        if (raw == null) { value = default; return true; }
        try
        {
            value = (T)Convert.ChangeType(raw, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
}
