using System;
using Adamantium.ECS.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools;

internal static class ScreenSpace
{
    public static float UnitsPerPixel(CameraBase camera, Vector3 worldPoint)
    {
        var projection = camera.ProjectionMatrix;
        var depth = Vector3F.Dot((Vector3F)(worldPoint - camera.WorldPosition), (Vector3F)camera.Forward);
        var w = depth * projection.M34 + projection.M44;
        return 2f * Math.Abs(w) / (camera.Width * projection.M11);
    }

    public static Vector3F InRender(Vector3 worldPoint, CameraBase camera)
    {
        return (Vector3F)(worldPoint - camera.WorldPosition);
    }

    public static QuaternionF Facing(CameraBase camera)
    {
        return QuaternionF.Conjugate(camera.Rotation);
    }
}
