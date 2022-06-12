using Adamantium.Engine.Managers;
using Adamantium.Engine.Services;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Game.Core.Input;

namespace Adamantium.Engine.Tools
{
    public abstract class LightToolBase : ToolBase
    {
        protected float MinimumAllowedRange = 0.01f;

        protected Light CurrentLight { get; set; }

        protected LightToolBase(string name) : base(name)
        {
        }

        public virtual bool Process(Entity targetEntity, Light light, CameraManager cameraManager, GameInputManager inputManager)
        {
            CurrentLight = light;
            Process(targetEntity, cameraManager, inputManager);
            return toolIntersectionResult.Intersects || IsLocked;
        }

        public virtual void TransformTool(Entity target, Light light, CameraManager cameraManager, Camera activeCamera)
        {
            CurrentLight = light;
            UpdateToolTransform(target, cameraManager, true, true, true);
        }
    }
}