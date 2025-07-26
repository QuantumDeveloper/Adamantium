using Adamantium.Mathematics;

namespace Adamantium.UI.Core;

public static class RootVisualExtensions
{
    public static Matrix4x4F GetProjectionMatrix(this IRootVisualComponent visualRoot)
    {
       return Matrix4x4F.OrthoOffCenter(
            0, 
            (float)visualRoot.ClientWidth, 
            0, 
            (float)visualRoot.ClientHeight,
            0f,
            100000f);
    }
}