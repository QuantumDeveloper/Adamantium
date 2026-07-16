using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Graphics;

public class RenderData
{
    public RenderData(float opacity, Matrix4x4F transform, bool clipToBounds, Rect clipRect)
    {
        TransformMatrix = transform;
        Opacity = opacity;
        ClipToBounds = clipToBounds;
        ClipRect = clipRect;
    }

    // Set at RECORD from the element's own opacity, then OVERWRITTEN at bake with the effective opacity composed from the
    // frozen snapshot chain (so a paint-only opacity change re-bakes without a re-record). See RenderCache.EffectiveOpacity.
    public float Opacity { get; set; }
    
    public Matrix4x4F TransformMatrix { get; set; }
    
    public bool ClipToBounds { get; }
    
    public Rect ClipRect { get; }
    
    public Matrix4x4F ProjectionMatrix { get; set; }

    // Viewport zoom multiplier (designer renders ClientSize x RenderScale; the projection stays logical). 1 = on-screen
    // 1:1. Analytic AA reads it so the fringe stays ~1 DEVICE px under zoom (TransformMatrix carries only the logical
    // local->world scale; the zoom lives in the viewport, not the projection - see WindowRendererBase.RenderScale).
    public double RenderScale { get; set; } = 1.0;

    public Effect CustomEffect { get; }
}