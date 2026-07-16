using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Graphics;

public interface IRenderUnit : IDisposable
{
    IUIComponent Component { get; }
    /// <summary>Set the effective alpha (element opacity composed down the tree) the unit's colours bake with, before a
    /// bake/re-bake. The draw path composes it from the frozen snapshot, not the live property.</summary>
    void SetEffectiveOpacity(float opacity);
    void DeferDispose();
    void Update(Matrix4x4F transform, Matrix4x4F projection, double renderScale);
    /// <summary>Out-of-render-pass work recorded before BeginRendering (e.g. shared-surface latch copies).</summary>
    void PreRender();
    void Render();
    void UpdateWithDrawCommand(IDrawCommand command);
    bool Match(IDrawCommand drawCommand);
}