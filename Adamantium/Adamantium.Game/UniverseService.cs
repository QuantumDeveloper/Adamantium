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
        _universes = [];
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
                if (item.Service != renderService)
                {
                    return;
                }

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
                if (!output.IsVisible)
                {
                    continue;
                }

                output.CopyOutput(graphicsDevice);
            }
        }
    }

    public event Action<IUniverse> OnUniverseAdded;

    private sealed record UniverseKey(string Name, IWindow Window, EntityService Service, IUniverse Universe);
}
