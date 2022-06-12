using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.EntityFramework;

namespace Adamantium.Game.Core;

public abstract class GameManagerBase : PropertyChangedBase
{
    private bool enabled;

    protected GameManagerBase(IGame game)
    {
        Game = game;
        EntityWorld = game.EntityWorld;
        DependencyResolver = game.Resolver;
        EventAggregator = DependencyResolver.Resolve<IEventAggregator>();
    }

    public bool Enabled
    {
        get => enabled;
        set => SetProperty(ref enabled, value);
    }
    
    protected IGame Game { get; }
    
    protected EntityWorld EntityWorld { get; }
    
    protected IDependencyResolver DependencyResolver { get; }
    
    protected IEventAggregator EventAggregator { get; }

    public virtual void Update(AppTime gameTime)
    {
        
    }
}