using Adamantium.Engine.Managers;
using Adamantium.Engine.Services;
using Adamantium.Engine.Templates.Lights;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components.Extensions;
using Adamantium.Game.Core.Input;

namespace Adamantium.Engine.Tools
{
    public class DirectionalLightTool : LightToolBase
    {
        public DirectionalLightTool(string name) : base(name)
        {
            Tool = new DirectionalLightVisualTemplate().BuildEntity(null, "Directional");
        }

        public override void Process(Entity targetEntity, CameraManager cameraManager, GameInputManager inputManager)
        {
            if (!CheckTargetEntity(targetEntity))
                return;

            HighlightSelectedTool(false);
            var camera = cameraManager.UserControlledCamera;

            SetIsLocked(inputManager);

            if (!IsLocked)
            {
                Tool.IsEnabled = true;
                UpdateToolTransform(targetEntity, cameraManager, false, true, true);

                var collisionMode = CollisionMode.CollidersOnly;

                toolIntersectionResult = Tool.Intersects(
                    camera,
                    inputManager.RelativePosition,
                    collisionMode,
                    CompareOrder.Less,
                    0.05f);

                if (toolIntersectionResult.Intersects)
                {
                    selectedTool = toolIntersectionResult.Entity;
                    previousCoordinates = toolIntersectionResult.IntersectionPoint;
                    HighlightSelectedTool(true);
                }

                IsLocked = CheckIsLocked(inputManager);

                if (IsLocked)
                {
                    HighlightSelectedTool(true);
                }
                else
                {
                    ShouldStayVisible(inputManager);
                }
            }

            Transform(Tool, cameraManager);
        }
    }
}
