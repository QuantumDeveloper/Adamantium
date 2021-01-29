using Adamantium.Engine.Services;
using Adamantium.Engine.Templates.Lights;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components.Extensions;
using Adamantium.Game.GameInput;

namespace Adamantium.Engine.Tools
{
    public class DirectionalLightTool : LightTool
    {
        public DirectionalLightTool(string name) : base(name)
        {
            Tool = new DirectionalLightVisualTemplate().BuildEntity(null, "Directional");
        }

        public override void Process(Entity targetEntity, CameraService cameraService, InputService inputService)
        {
            if (!CheckTargetEntity(targetEntity))
                return;

            HighlightSelectedTool(false);
            var camera = cameraService.UserControlledCamera;

            SetIsLocked(inputService);

            if (!IsLocked)
            {
                Tool.IsEnabled = true;
                UpdateToolTransform(targetEntity, cameraService, false, true, true);

                var collisionMode = CollisionMode.CollidersOnly;

                toolIntersectionResult = Tool.Intersects(
                    camera,
                    inputService.RelativePosition,
                    collisionMode,
                    CompareOrder.Less,
                    0.05f);

                if (toolIntersectionResult.Intersects)
                {
                    selectedTool = toolIntersectionResult.Entity;
                    previousCoordinates = toolIntersectionResult.IntersectionPoint;
                    HighlightSelectedTool(true);
                }

                IsLocked = CheckIsLocked(inputService);

                if (IsLocked)
                {
                    HighlightSelectedTool(true);
                }
                else
                {
                    ShouldStayVisible(inputService);
                }
            }

            Transform(Tool, cameraService);
        }
    }
}
