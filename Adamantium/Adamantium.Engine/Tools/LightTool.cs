using Adamantium.Engine.Services;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Game.GameInput;

namespace Adamantium.Engine.Tools
{
    public abstract class LightTool : TransformTool
    {
        protected float MinimumAllowedRange = 0.01f;

        protected Light CurrentLight { get; set; }

        protected LightTool(string name) : base(name)
        {
        }

        public virtual bool Process(Entity targetEntity, Light light, CameraService cameraService, InputService inputService)
        {
            CurrentLight = light;
            Process(targetEntity, cameraService, inputService);
            return toolIntersectionResult.Intersects || IsLocked;
        }

        public virtual void TransformTool(Entity target, Light light, CameraService cameraService, Camera activeCamera)
        {
            CurrentLight = light;
            UpdateToolTransform(target, cameraService, true, true, true);
        }
    }
}