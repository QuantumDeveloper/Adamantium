using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Graphics;

public class RenderData
{
    public RenderData(float opacity, Vector2 location, bool clipToBounds, Rect clipRect)
    {
        Opacity = opacity;
        Location = location;
        ClipToBounds = clipToBounds;
        ClipRect = clipRect;
    }

    public float Opacity { get; }
    
    public Vector2 Location { get; }
    
    public bool ClipToBounds { get; }
    
    public Rect ClipRect { get; }
    
    public Matrix4x4F ProjectionMatrix { get; set; }
    
    public Effect CustomEffect { get; }
}