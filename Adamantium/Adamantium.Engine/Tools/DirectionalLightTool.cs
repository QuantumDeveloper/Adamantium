using Adamantium.Engine.Services;
using Adamantium.Engine.Templates.Lights;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.ECS.Components.Extensions;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Input;

namespace Adamantium.Engine.Tools;

public class DirectionalLightTool : LightToolBase
{
    public DirectionalLightTool(string name) : base(name)
    {
        Tool = new DirectionalLightVisualTemplate().BuildEntity(null, "Directional");
    }

    public override void Process(Entity targetEntity, Observatory observatory)
    {
        var output = observatory.PointerOutput;
        var inputManager = output.Input;
        if (!CheckTargetEntity(targetEntity))
            return;

        HighlightSelectedTool(false);
        var camera = output.Camera;

        SetIsLocked(inputManager);

        if (!IsLocked)
        {
            Tool.IsEnabled = true;
            UpdateToolTransform(targetEntity, camera,false, true, true);

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

        Transform(Tool, observatory);
    }
}