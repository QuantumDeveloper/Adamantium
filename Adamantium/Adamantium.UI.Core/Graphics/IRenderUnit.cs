using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Graphics;

public interface IRenderUnit : IDisposable
{
    IUIComponent Component { get; }

    /// <summary>The per-command state captured at RECORD time (opacity, transform, clip, halo bands). The draw path
    /// reads VALUES from here; the live element is edited on another thread and must never be dereferenced there.</summary>
    RenderData RenderData { get; }
    /// <summary>Set the alpha the unit's colours bake with - the element's OWN opacity, not the chain. The chain rides
    /// <see cref="FadeSlot"/> and is applied at draw time; folding it in here would mean re-baking every instance under
    /// a fading container.</summary>
    void SetEffectiveOpacity(float opacity);

    /// <summary>The opacity slot this unit's instances read their fade from (-1 = nothing above it fades). Resolved by
    /// the draw walk and carried into every instance it bakes.</summary>
    int FadeSlot { get; }

    void SetFadeSlot(int slot);
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