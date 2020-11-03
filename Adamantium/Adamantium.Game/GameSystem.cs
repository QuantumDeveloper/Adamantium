using Adamantium.Core.DependencyInjection;
using Adamantium.Engine.Core;

namespace Adamantium.Game
{
    public abstract class GameSystem : SystemCore
    {
        protected GameBase Game { get; private set; }

        protected GameSystem(GameBase game, IDependencyContainer container)
            : base(container)
        {
            Game = game;
        }
    }
}
