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
    IDrawingSession DrawText(TextRenderingParameters renderingParameters, 
        Size desiredSize, 
        TextLayout textLayout,
        Brush foreground, 
        Brush background,
        Brush stroke);
    IDrawingSession PushImage(ImageSource image);
}