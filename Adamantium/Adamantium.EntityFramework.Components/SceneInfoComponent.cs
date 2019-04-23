using Adamantium.Engine.Core.Models;
using Adamantium.EntityFramework.ComponentsBasics;

namespace Adamantium.EntityFramework.Components
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
