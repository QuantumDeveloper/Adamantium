using Adamantium.Engine.Services;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.Game.Core;

namespace Adamantium.Engine.Tools;

public abstract class LightToolBase : ToolBase
{
    protected float MinimumAllowedRange = 0.01f;

    protected Light CurrentLight { get; set; }

    protected LightToolBase(string name) : base(name)
    {
    }

    public virtual bool Process(Entity targetEntity, Light light, Observatory observatory)
    {
        CurrentLight = light;
        Process(targetEntity, observatory);
        return toolIntersectionResult.Intersects || IsLocked;
    }

    public virtual void TransformTool(Entity target, Light light, Camera activeCamera)
    {
        CurrentLight = light;
        UpdateToolTransform(target, activeCamera, true, true, true);
    }
}
