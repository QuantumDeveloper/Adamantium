using System;
using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.UI;

namespace Adamantium.Game.Core
{
    public interface IGame : IService
    {
        EntityWorld EntityWorld { get; }
        
        IReadOnlyList<GameOutput> Outputs { get; }
        
        GameOutput ActiveOutput { get; }
        
        GameOutput MainOutput { get; }

        public void InitializeGame();
        
        /// <summary>
        /// Game services which could be added to the game
        /// </summary>
        public IDependencyContainer Container { get; }

        /// <summary>
        /// Enables or disables fixed framerate
        /// </summary>
        public Boolean IsFixedTimeStep { get; set; }

        /// <summary>
        /// Gets or set time step for limitation of rendering frequency
        /// <remarks>value must be in seconds</remarks>
        /// </summary>
        public Double TimeStep { get; }
        
        /// <summary>
        /// Desired number of frames per second
        /// </summary>
        UInt32 DesiredFPS { get; set; }
        
        /// <summary>
        /// Condition on which game loop will be exited
        /// </summary>
        ShutDownMode ShutDownMode { get; set; }
        
        GameMode Mode { get; }
        
        /// <summary>
        /// Title of the game to show in the window title bar
        /// </summary>
        String Title { get; set; }

        void Run(object context, GraphicsDevice graphicsDevice);
        
        public event EventHandler Initialized;

        public event EventHandler FrameFinished;
    }
}