using System;
using System.Collections.Concurrent;

namespace Adamantium.Core;

/// <summary>
/// The one-of-a-kind objects that belong to their owner - a universe, an application - and live as long as it does.
/// Not a container: nothing is built here, only instances the owner put in, found by the type they were put in under.
/// </summary>
public sealed class Satellites
{
    private readonly ConcurrentDictionary<Type, object> items = new();

    /// <summary>
    /// Puts <paramref name="satellite"/> in under <typeparamref name="T"/>; throws when one is already there.
    /// </summary>
    public void Add<T>(T satellite) where T : class
    {
        if (satellite == null)
        {
            throw new ArgumentNullException(nameof(satellite));
        }

        if (!items.TryAdd(typeof(T), satellite))
        {
            throw new InvalidOperationException($"{typeof(T).Name} is already among the satellites.");
        }
    }

    /// <summary>
    /// The satellite put in under <typeparamref name="T"/>; throws when there is none.
    /// </summary>
    public T Get<T>() where T : class
    {
        if (items.TryGetValue(typeof(T), out var satellite))
        {
            return (T)satellite;
        }

        throw new InvalidOperationException($"{typeof(T).Name} is not among the satellites.");
    }
}
