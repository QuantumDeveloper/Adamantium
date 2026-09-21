using Adamantium.Mathematics;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Core.Media.Drawings;

/// <summary>A raster picture placed inside a drawing - a photo in a vector frame, a logo among drawn shapes.</summary>
public class ImageDrawing : Drawing
{
    public static readonly AdamantiumProperty ImageSourceProperty = AdamantiumProperty.Register(nameof(ImageSource),
        typeof(ImageSource), typeof(ImageDrawing), new PropertyMetadata(null));

    public static readonly AdamantiumProperty RectProperty = AdamantiumProperty.Register(nameof(Rect),
        typeof(Rect), typeof(ImageDrawing), new PropertyMetadata(default(Rect)));

    /// <summary>The picture to draw.</summary>
    public ImageSource ImageSource
    {
        get => GetValue<ImageSource>(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    /// <summary>Where it goes, in the drawing's own coordinates.</summary>
    public Rect Rect
    {
        get => GetValue<Rect>(RectProperty);
        set => SetValue(RectProperty, value);
    }

    protected override void AttachChildren() => AttachOwned(ImageSource);

    public override Rect Bounds => Rect;

    public override void Render(IDrawingSession session, Matrix4x4F transform)
    {
        var image = ImageSource;
        if (image == null) return;

        // The image path draws into an axis-aligned destination rect, so the placement is the transformed BOX. A rotated
        // or sheared enclosing group therefore lays this child out upright inside its rotated extent instead of turning
        // it - the geometry children of the same group DO rotate. Turning a raster child needs the image draw to carry a
        // matrix the way DrawGeometry now does; until it does, this is the honest limit rather than a silent wrong picture.
        session.DrawImage(image, null, Rect.TransformToAABB(transform), default);
    }
}
