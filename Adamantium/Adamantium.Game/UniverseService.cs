using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.Game.Core;
using Adamantium.Graphics.Core;
using Adamantium.UI.Core;

namespace Adamantium.Game;

public class UniverseService : IUniverseService
{
    private readonly object _locker = new object();

    private List<UniverseKey> _universes;

    public UniverseService()
    {
        _universes = new List<UniverseKey>();
    }

    public IReadOnlyList<IUniverse> Universes
    {
        get
        {
            lock (_locker)
            {
                return _universes.Select(x=>x.Universe).ToList();
            }
        }
    }

    public T CreateUniverse<T>(string name, IWindow wnd, EntityService service, params object[] args) where T : IUniverse
    {
        var universe = (T)Activator.CreateInstance(typeof(T), args);
        universe.InitializeUniverse();
        var key = new UniverseKey(name, wnd, service, universe);
        lock (_locker)
        {
            _universes.Add(key);
        }

        OnUniverseAdded?.Invoke(universe);
        return universe;
    }

    public bool RemoveUniverse(IUniverse universe)
    {
        lock (_locker)
        {
            var result = _universes.FirstOrDefault(x => x.Universe == universe);
            return _universes.Remove(result);
        }
    }

    public void RunUniverses(IRenderService renderService, AppTime time)
    {
        lock (_locker)
        {
            Parallel.ForEach(_universes, (item) =>
            {
                if (item.Service != renderService)  return;

                item.Universe.RunOnce(time);
            });
            foreach (var key in _universes)
            {
                key.Universe.Submit();
            }
        }
    }

    public void CopyOutput(IGraphicsDevice graphicsDevice)
    {
        foreach (var universe in Universes)
        {
            foreach (var output in universe.Outputs)
            {
                // A whole frame's worth of pixels into a surface nobody samples: an output off the visual tree - a game
                // panel whose tab is not the selected one - drew nothing this frame and has no viewer for it either.
                if (!output.IsVisible) continue;

                output.CopyOutput(graphicsDevice);
            }
        }
    }

    public event Action<IUniverse> OnUniverseAdded;

    private class UniverseKey
    {
        public UniverseKey(string name, IWindow window, EntityService service, IUniverse universe)
        {
            Name = name;
            Window = window;
            Service = service;
            Universe = universe;
        }

        public string Name { get; }

        public IWindow Window { get; }

        public EntityService Service { get; }

        public IUniverse Universe { get; }

        public override bool Equals(object obj)
        {
            if (obj is UniverseKey key)
            {
                return this == key;
            }

            return false;
        }

        public static bool operator == (UniverseKey key1, UniverseKey key2)
        {
            if (key1 == null || key2 == null) return false;

            return key1 == key2;
        }

        public static bool operator !=(UniverseKey key1, UniverseKey key2)
        {
            return !(key1 == key2);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name.GetHashCode(), Window.GetHashCode(), Service.GetHashCode(), Universe.GetHashCode());
        }
    }
}
