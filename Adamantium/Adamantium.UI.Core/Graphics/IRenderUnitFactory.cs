using Adamantium.Graphics.Core;

namespace Adamantium.UI.Core.Graphics;

public interface IRenderUnitFactory
{
    void RegisterFactory<T>(Func<IDrawCommand, IRenderUnit> factory);

    IRenderUnit CreateRenderUnitFromCommand(IDrawCommand command);

    /// <summary>The device the units this factory builds are created on. Null in the GPU-free test renderer, which builds
    /// no device resources at all.</summary>
    IGraphicsDevice GraphicsDevice { get; }
}