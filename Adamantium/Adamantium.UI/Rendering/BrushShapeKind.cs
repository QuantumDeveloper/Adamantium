namespace Adamantium.UI.Rendering;

/// <summary>The shapes a brush batch can paint on. The numbers match what the pixel shaders read (see
/// <c>BrushShapeDistance</c>), so a value here IS the shader's shape selector.</summary>
internal enum BrushShapeKind
{
    RoundedRect = 0,
    Ellipse = 1,
    Polygon = 2
}
