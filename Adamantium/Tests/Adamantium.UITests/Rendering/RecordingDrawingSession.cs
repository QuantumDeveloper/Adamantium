using System.Collections.Generic;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UITests.Rendering;

/// <summary>Captures what a drawing replays, so a test can assert the CALLS a Drawing makes without a GPU.</summary>
internal sealed class RecordingDrawingSession : IDrawingSession
{
    public List<(Brush Brush, Geometry Geometry, Pen Pen, Matrix4x4F Transform)> Geometries { get; } = [];

    public List<(ImageSource Image, Rect Destination)> Images { get; } = [];

    public IDrawingSession DrawGeometry(Brush brush, Geometry geometry, Pen pen, Matrix4x4F transform)
    {
        Geometries.Add((brush, geometry, pen, transform));
        return this;
    }

    public IDrawingSession DrawGeometry(Brush brush, Geometry geometry, Pen pen = null) =>
        DrawGeometry(brush, geometry, pen, Matrix4x4F.Identity);

    public IDrawingSession DrawImage(ImageSource image, Brush filter, Rect destinationRect, CornerRadius corners)
    {
        Images.Add((image, destinationRect));
        return this;
    }

    public IDrawingSession DrawImage(ImageSource image, Brush filter, Rect destinationRect, CornerRadius corners, Rect sourceUv) =>
        DrawImage(image, filter, destinationRect, corners);

    public IDrawingSession DrawImageFrame(ImageSource image, Brush filter, Rect destinationRect, CornerRadius corners, int frameLayer) =>
        DrawImage(image, filter, destinationRect, corners);

    public IDrawingSession DrawLine(Vector2 start, Vector2 end, Pen pen) => this;

    public IDrawingSession DrawRectangle(Brush brush, Rect destinationRect, Pen pen = null) => this;

    public IDrawingSession DrawRectangle(Brush brush, Rect destinationRect, CornerRadius corners, Pen pen = null) => this;

    public IDrawingSession DrawBorder(Brush background, Rect destinationRect, CornerRadius corners, Brush borderBrush,
        Thickness borderThickness) => this;

    public IDrawingSession DrawEllipse(Rect destinationRect, Brush brush, double startAngle, double sweepAngle,
        EllipseType ellipseType, Pen pen = null, double ringThickness = 0) => this;

    public List<(TextLayout Layout, Matrix4x4F Transform, double Width)> Texts { get; } = [];

    public IDrawingSession DrawText(TextRenderingParameters renderingParameters, Size desiredSize, TextLayout textLayout,
        Brush foreground, Brush background, Brush stroke, Matrix4x4F transform)
    {
        Texts.Add((textLayout, transform, desiredSize.Width));
        return this;
    }

    public IDrawingSession DrawText(TextRenderingParameters renderingParameters, Size desiredSize, TextLayout textLayout,
        Brush foreground, Brush background, Brush stroke) => this;

    public IDrawingSession PushImage(ImageSource image) => this;
}
