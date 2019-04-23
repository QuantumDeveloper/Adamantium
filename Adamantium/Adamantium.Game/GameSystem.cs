using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Adamantium.Engine.Core;

namespace Adamantium.Engine
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
