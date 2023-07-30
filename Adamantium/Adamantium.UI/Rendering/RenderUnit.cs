using System;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media.Imaging;

namespace Adamantium.UI.Rendering;

internal class RenderUnit : DisposableObject
{
    private GeometryRenderer geometryRenderer;
    private GeometryRenderer strokeRenderer;

    public RenderUnit()
    {
        GeometryMetadata = new GeometryMetadata();
    }
    
    public ImageSource Image { get; set; }

    public GeometryRenderer GeometryRenderer
    {
        get => geometryRenderer;
        set => geometryRenderer = ToDispose(value);
    }

    public GeometryRenderer StrokeRenderer
    {
        get => strokeRenderer;
        set => strokeRenderer = ToDispose(value);
    }
    
    public GeometryMetadata GeometryMetadata { get; set; }

    protected override void Dispose(bool disposeManagedResources)
    {
        base.Dispose(disposeManagedResources);
            
        GeometryRenderer?.Dispose();
        StrokeRenderer?.Dispose();
    }

    public void Draw(GraphicsDevice graphicsDevice, IUIComponent component, Matrix4x4F projectionMatrix)
    {
        geometryRenderer?.Draw(graphicsDevice, component, Image, projectionMatrix);
        strokeRenderer?.Draw(graphicsDevice, component, projectionMatrix);
    }
}

internal class GeometryMetadata : IEquatable<GeometryMetadata>
{
    public Rect Rectangle;
    public CornerRadius Corners;

    public double StartAngle;
    public double StopAngle;

    public bool Equals(GeometryMetadata other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Rectangle.Equals(other.Rectangle) && Corners.Equals(other.Corners);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((GeometryMetadata)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Rectangle, Corners);
    }
}