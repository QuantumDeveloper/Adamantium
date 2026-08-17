using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Core.Graphics;

public interface IDrawingSession
{
    IDrawingSession DrawLine(Vector2 start, Vector2 end, Pen pen);
    IDrawingSession DrawRectangle(Brush brush, Rect destinationRect, Pen pen = null);
    IDrawingSession DrawRectangle(Brush brush, Rect destinationRect, CornerRadius corners, Pen pen = null);

    /// <summary>A filled rect with a BORDER drawn inside it - each side its own thickness, each corner its own radius.
    /// <para>Not a pen: a pen is one width offset from a contour, and four different widths are not an offset of
    /// anything. Fill and border are ONE draw on purpose - as two shapes they share an outline, both anti-alias it, and
    /// the two halves of that edge composite into a dark hairline all the way round.</para></summary>
    IDrawingSession DrawBorder(Brush background, Rect destinationRect, CornerRadius corners, Brush borderBrush, Thickness borderThickness);
    IDrawingSession DrawEllipse(Rect destinationRect, Brush brush, Double startAngle, Double sweepAngle, EllipseType ellipseType, Pen pen = null);
    IDrawingSession DrawGeometry(Brush brush, Geometry geometry, Pen pen = null);

    /// <summary>Draws the geometry placed by <paramref name="transform"/>, WITHOUT touching the geometry itself. A
    /// <see cref="Media.Drawings.Drawing"/> replays this way: the same shape appears at several sizes and positions in
    /// one frame, and each placement must stay a separate instance of ONE shared mesh. Baking the placement into the
    /// geometry instead would give every size its own tessellation and defeat the instancing outright.</summary>
    IDrawingSession DrawGeometry(Brush brush, Geometry geometry, Pen pen, Matrix4x4F transform);
    IDrawingSession DrawImage(ImageSource image, Brush filter, Rect destinationRect, CornerRadius corners);

    /// <summary>Draws a normalised (0..1) SUB-RECT of the image into <paramref name="destinationRect"/> - a mosaic tile
    /// shows just its fragment of one shared photo without cropping/copying the bitmap.</summary>
    IDrawingSession DrawImage(ImageSource image, Brush filter, Rect destinationRect, CornerRadius corners, Rect sourceUv);

    /// <summary>Draws one FRAME of an animated image: its frames are the layers of a single texture, and the frame is
    /// chosen in the shader. Advancing an animation therefore uploads nothing and allocates nothing.</summary>
    IDrawingSession DrawImageFrame(ImageSource image, Brush filter, Rect destinationRect, CornerRadius corners, int frameLayer);
    IDrawingSession DrawText(TextRenderingParameters renderingParameters, 
        Size desiredSize, 
        TextLayout textLayout,
        Brush foreground, 
        Brush background,
        Brush stroke);
    /// <summary>Draws the text run placed by <paramref name="transform"/>. Needed for the same reason DrawGeometry has
    /// one: a <see cref="Media.Drawings.Drawing"/> puts several runs at their own spots inside a single element, and the
    /// text area cannot say where - it aligns the run WITHIN the layout, while the quad itself is placed by the unit.</summary>
    IDrawingSession DrawText(TextRenderingParameters renderingParameters,
        Size desiredSize,
        TextLayout textLayout,
        Brush foreground,
        Brush background,
        Brush stroke,
        Matrix4x4F transform);

    IDrawingSession PushImage(ImageSource image);
}