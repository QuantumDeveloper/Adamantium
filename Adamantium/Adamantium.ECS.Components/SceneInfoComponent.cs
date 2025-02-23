using Adamantium.ECS.ComponentsBasics;
using Adamantium.Graphics.Core.Models;

namespace Adamantium.ECS.Components
{
    public class SceneInfoComponent : Component
   {
      public SceneInfoComponent()
      {
         Cameras = new SceneData.CameraCollection();
         Lights = new SceneData.LightCollection();
      }
      public SceneData.CameraCollection Cameras { get; set; }
      public SceneData.LightCollection Lights { get; set; }
   }
}
