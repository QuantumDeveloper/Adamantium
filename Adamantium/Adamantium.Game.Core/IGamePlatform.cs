using Adamantium.Engine.Graphics;
using Adamantium.Imaging;

namespace Adamantium.Game.Core
{
    public interface IGamePlatform
    {
        String DefaultAppDirectory { get; }

        GameOutput MainWindow { get; }

        GameOutput ActiveWindow { get; }

        IReadOnlyList<GameOutput> Outputs { get; }
        
        bool HasOutputs { get; }

        void Run(CancellationToken token);
        
        /// <summary>
        /// Creates <see cref="GameOutput"/> window from width and height
        /// <param name="width">Window width</param>
        /// <param name="height">Window height</param>
        /// </summary>
        GameOutput CreateOutput(uint width = 1280, uint height = 720);

        /// <summary>
        /// Creates <see cref="GameOutput"/> from <see cref="GameContext"/>
        /// </summary>
        /// <param name="context">Context (Control) from which <see cref="GameOutput"/> will be created</param>
        /// <returns>new <see cref="GameOutput"/></returns>
        GameOutput CreateOutput(GameContext context);

        /// <summary>
        /// Creates <see cref="GameOutput"/> from <see cref="object"/>
        /// </summary>
        /// <param name="context">Context (Control) from which <see cref="GameOutput"/> will be created</param>
        /// <returns>new <see cref="GameOutput"/></returns>
        GameOutput CreateOutput(Object context);

        /// <summary>
        /// Create new game window from context (if no windows has been created already using this context) and add it to the list of game windows
        /// </summary>
        /// <param name="context">Window, in which DX xontent will be rendered</param>
        /// <param name="surfaceFormat">Surface format</param>
        /// <param name="depthFormat">Depth buffer format</param>
        /// <param name="msaaLevel">MSAA level</param>
        GameOutput CreateOutput(Object context, SurfaceFormat surfaceFormat, DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, MSAALevel msaaLevel = MSAALevel.None);

        /// <summary>
        /// Switches drawing context from old control to new control. After this old control could be safely removed
        /// </summary>
        /// <param name="oldContext">Old control for drawing</param>
        /// <param name="newContext">New control for drawing</param>
        void SwitchContext(GameContext oldContext, GameContext newContext);

        /// <summary>
        /// Adds <see cref="GameOutput"/> to the windows collection
        /// </summary>
        /// <param name="window">window to add to the windows collection</param>
        void AddOutput(GameOutput window);
        
        /// <summary>
        /// Removes <see cref="GameOutput"/> from <see cref="GameOutput"/>
        /// </summary>
        /// <param name="context">Context (Control) by which <see cref="GameOutput"/> will be removed</param>
        void RemoveOutput(GameContext context);

        /// <summary>
        /// Remove <see cref="GameOutput"/>
        /// </summary>
        /// <param name="context">UI Control for which <see cref="GameOutput"/> will be removed</param>
        void RemoveOutput(Object context);
    }
}
