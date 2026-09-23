using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;

namespace Adamantium.Game.Core
{
    public interface IUniversePlatform
    {
        String DefaultAppDirectory { get; }

        UniverseOutput MainWindow { get; }

        UniverseOutput ActiveWindow { get; }

        IReadOnlyList<UniverseOutput> Outputs { get; }
        
        bool HasOutputs { get; }

        void Run(CancellationToken token);
        
        /// <summary>
        /// Creates <see cref="UniverseOutput"/> window from width and height
        /// <param name="width">Window width</param>
        /// <param name="height">Window height</param>
        /// </summary>
        UniverseOutput CreateOutput(uint width = 1280, uint height = 720);

        /// <summary>
        /// Creates <see cref="UniverseOutput"/> from <see cref="OutputContext"/>
        /// </summary>
        /// <param name="context">Context (Control) from which <see cref="UniverseOutput"/> will be created</param>
        /// <returns>new <see cref="UniverseOutput"/></returns>
        UniverseOutput CreateOutput(OutputContext context);

        /// <summary>
        /// Creates <see cref="UniverseOutput"/> from <see cref="object"/>
        /// </summary>
        /// <param name="context">Context (Control) from which <see cref="UniverseOutput"/> will be created</param>
        /// <returns>new <see cref="UniverseOutput"/></returns>
        UniverseOutput CreateOutput(Object context);

        /// <summary>
        /// Create new game window from context (if no windows has been created already using this context) and add it to the list of game windows
        /// </summary>
        /// <param name="context">Window, in which DX xontent will be rendered</param>
        /// <param name="surfaceFormat">Surface format</param>
        /// <param name="depthFormat">Depth buffer format</param>
        /// <param name="msaaLevel">MSAA level</param>
        UniverseOutput CreateOutput(Object context, SurfaceFormat surfaceFormat, DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, MSAALevel msaaLevel = MSAALevel.None);

        /// <summary>
        /// Switches drawing context from old control to new control. After this old control could be safely removed
        /// </summary>
        /// <param name="oldContext">Old control for drawing</param>
        /// <param name="newContext">New control for drawing</param>
        void SwitchContext(OutputContext oldContext, OutputContext newContext);

        /// <summary>
        /// Adds <see cref="UniverseOutput"/> to the windows collection
        /// </summary>
        /// <param name="window">window to add to the windows collection</param>
        void AddOutput(UniverseOutput window);
        
        /// <summary>
        /// Removes <see cref="UniverseOutput"/> from <see cref="UniverseOutput"/>
        /// </summary>
        /// <param name="context">Context (Control) by which <see cref="UniverseOutput"/> will be removed</param>
        void RemoveOutput(OutputContext context);

        /// <summary>
        /// Remove <see cref="UniverseOutput"/>
        /// </summary>
        /// <param name="context">UI Control for which <see cref="UniverseOutput"/> will be removed</param>
        void RemoveOutput(Object context);
    }
}
