using Adamantium.Engine.Core;

namespace Adamantium.Game
{
    public abstract class GameSystem : SystemCore
    {
        protected GameBase Game { get; private set; }

        protected GameSystem(GameBase game, IServiceStorage storage)
            : base(storage)
        {
            Game = game;
        }
    }
}
