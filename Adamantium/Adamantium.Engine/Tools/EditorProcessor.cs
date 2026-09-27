using Adamantium.ECS;
using Adamantium.ECS.Components.Extensions;
using Adamantium.Engine.EntityServices;

namespace Adamantium.Engine.Tools;

/// <summary>
/// Editor work that <see cref="ToolsService"/> runs every frame, and that may draw over the scene of every output.
/// </summary>
public abstract class EditorProcessor : EntityProcessor<ToolsService>
{
    protected ToolsService Tools => AssociatedService;

    /// <summary>Draws into one output's frame, after its scene.</summary>
    public virtual void DrawOverlay(EditorOverlayProcessor overlay)
    {
    }

    /// <summary>The scene entity a click along <paramref name="ray"/> would select through what this draws, if any.</summary>
    public virtual PickHit PickEntity(in PickRay ray)
    {
        return default;
    }
}
