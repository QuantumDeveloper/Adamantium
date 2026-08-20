using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Graphics;

public interface IRenderUnit : IDisposable
{
    IUIComponent Component { get; }

    /// <summary>The per-command state captured at RECORD time (opacity, transform, clip, halo bands). The draw path
    /// reads VALUES from here; the live element is edited on another thread and must never be dereferenced there.</summary>
    RenderData RenderData { get; }
    /// <summary>Set the effective alpha (element opacity composed down the tree) the unit's colours bake with, before a
    /// bake/re-bake. The draw path composes it from the frozen snapshot, not the live property.</summary>
    void SetEffectiveOpacity(float opacity);
    void DeferDispose();
    void Update(Matrix4x4F transform, Matrix4x4F projection, double renderScale);
    /// <summary>Out-of-render-pass work recorded before BeginRendering (e.g. shared-surface latch copies).</summary>
    /// <summary>Has this unit anything to do out of the render pass? Asked before the call, because the sweep visits
    /// every unit of every group on every frame and for most of them the answer is no.</summary>
    bool NeedsPreRender { get; }

    void PreRender();
    void Render();
    void UpdateWithDrawCommand(IDrawCommand command);
    bool Match(IDrawCommand drawCommand);
}