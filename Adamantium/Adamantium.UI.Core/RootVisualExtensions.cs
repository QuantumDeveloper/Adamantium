using Adamantium.Mathematics;

namespace Adamantium.UI.Core;

public static class RootVisualExtensions
{
    public static Matrix4x4F GetProjectionMatrix(this IRootVisualComponent visualRoot)
    {
       // SYMMETRIC depth range: flat UI sits at z = 0, but a 3D-rotated element (a flipping tile) swings its vertices to
       // z = +-(its size) - with near = 0 the negative half was DEPTH-CLIPPED and the element vanished as soon as it
       // tilted. A symmetric range keeps rotated content inside NDC on both sides (and preserves real z for 3D layering).
       return Matrix4x4F.OrthoOffCenter(
            0,
            (float)visualRoot.ClientWidth,
            0,
            (float)visualRoot.ClientHeight,
            -100000f,
            100000f);
    }
}