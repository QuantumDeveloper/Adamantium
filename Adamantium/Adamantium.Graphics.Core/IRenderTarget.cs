namespace Adamantium.Graphics.Core;

public interface IRenderTarget : ITexture
{
    ITexture ResolveTexture { get; }
}