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

    public float Opacity { get; }
    
    public Matrix4x4F TransformMatrix { get; set; }
    
    public bool ClipToBounds { get; }
    
    public Rect ClipRect { get; }
    
    public Matrix4x4F ProjectionMatrix { get; set; }
    
    public Effect CustomEffect { get; }
}