using System.Collections.Generic;
using Adamantium.ECS.Components;
using Adamantium.Game.Core;

namespace Adamantium.Engine;

public static class UniverseCameras
{
    /// <summary>
    /// Fills <paramref name="cameras"/> with the camera each visible output looks through now - once, however many
    /// outputs share it. A viewpoint nobody sees is not in it.
    /// </summary>
    public static void CollectCurrentCameras(this IUniverse universe, List<Camera> cameras)
    {
        cameras.Clear();
        var outputs = universe.Outputs;
        for (int i = 0; i < outputs.Count; i++)
        {
            if (!outputs[i].IsVisible)
            {
                continue;
            }

            var camera = outputs[i].Camera;
            if (camera != null && !cameras.Contains(camera))
            {
                cameras.Add(camera);
            }
        }
    }
}
