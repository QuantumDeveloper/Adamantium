namespace Adamantium.UI.Core.Media;

/// <summary>How a brush's rectangle is measured. WPF's <c>BrushMappingMode</c>: the same numbers mean a FRACTION of the
/// shape being filled, or device-independent pixels, and which one is meant cannot be guessed from the value.</summary>
public enum BrushMappingMode
{
    /// <summary>0..1 of the filled shape's bounding box. The default, and what makes a brush reusable across sizes.</summary>
    RelativeToBoundingBox,

    /// <summary>Logical pixels, independent of the shape's size - a tile that must stay 32px whatever it dresses.</summary>
    Absolute
}
