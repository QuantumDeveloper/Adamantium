namespace Adamantium.UI.Core.Graphics;

public interface IRenderCachePolicy
{
    bool RequiresBufferRebuild(IRenderCachePolicy newState);
}