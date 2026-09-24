using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.ECS;

namespace Adamantium.Game.Core
{
    public interface IUniverse : IAdamantiumApplication
    {
        EntityWorld EntityWorld { get; }

        /// <summary>
        /// What this universe has its own of, as opposed to what the whole process shares through <see cref="Container"/>.
        /// </summary>
        Satellites Satellites { get; }

        IReadOnlyList<UniverseOutput> Outputs { get; }
        
        UniverseOutput MainOutput { get; }

        public void InitializeUniverse();

        public void Submit();
        
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
        
        UniverseMode Mode { get; }
        
        /// <summary>
        /// Title of the game to show in the window title bar
        /// </summary>
        String Title { get; set; }

        void Run(object context);
        
        public event EventHandler Initialized;

        public event EventHandler FrameFinished;

        /// <summary>
        /// Raised once per frame, before the update, when the frame's outputs are settled: added, removed and resized.
        /// </summary>
        public event EventHandler OutputsSettled;
    }
}