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
    IDrawingSession DrawEllipse(Rect destinationRect, Brush brush, Double startAngle, Double sweepAngle, EllipseType ellipseType, Pen pen = null);
    IDrawingSession DrawGeometry(Brush brush, Geometry geometry, Pen pen = null);
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
    IDrawingSession PushImage(ImageSource image);
}