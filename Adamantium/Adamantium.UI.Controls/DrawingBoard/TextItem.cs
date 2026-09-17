using System;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Words on the plane: a piece of text at a place in the world, in a size measured in WORLD units - so it is
/// part of the drawing and grows with the zoom, the way a label written on paper does.
/// <para>Which is why it is shaped at the size it is DRAWN at rather than once: a glyph laid out for one size and then
/// scaled is a blurred glyph, and the whole reason this engine keeps text as glyphs is not to do that.</para></summary>
public class TextItem : ICanvasItem
{
    // TWO layouts, and they are not an optimisation - they are the fix for a real fault. A layout holds the shaping it
    // was last given, and the drawing keeps a reference to it: so when the box asked to be measured at the WORLD size
    // after the drawing had shaped it at the SCREEN size, the drawing came out at whatever the box had left behind.
    // Zoomed in the two differ, and the letters came out tiny inside a frame of the right size - which is exactly what
    // it looked like. Measuring now has a layout of its own and cannot disturb what is being drawn.
    private TextLayout _draw;
    private TextLayout _measure;
    private FontFamily _layoutFont;
    private string _shapedText;
    private double _shapedSize = double.NaN;
    private Size _measured;
    private string _drawnText;
    private double _drawnSize = double.NaN;
    private Size _drawn;

    public TextItem(Vector2 origin, string text, Brush brush, double fontSize, FontFamily font = null)
    {
        Origin = origin;
        Text = text ?? string.Empty;
        Brush = brush;
        FontSize = fontSize;
        FontFamily = font;
    }

    /// <summary>A second piece of text just like this one.</summary>
    public ICanvasItem Copy() => new TextItem(Origin, Text, Brush, FontSize, FontFamily);

    /// <summary>Where the text starts, in the world - its top-left corner.</summary>
    public Vector2 Origin { get; set; }

    public string Text { get; set; }

    public Brush Brush { get; set; }

    /// <summary>The height of the letters in WORLD units.</summary>
    public double FontSize { get; set; }

    /// <summary>The face, or nothing for the application's own.</summary>
    public FontFamily FontFamily { get; set; }

    /// <summary>How tall ONE line is, in world units - what a caret is drawn to. The box a piece of text covers is not
    /// that: it is as tall as however many lines there are, and for a caret the line is the answer.</summary>
    public double LineHeight
    {
        get
        {
            var lines = 1;
            foreach (var character in Text ?? string.Empty)
            {
                if (character == '\n') lines++;
            }

            var size = MeasureAt(FontSize);
            return size.Height > 0 ? size.Height / lines : FontSize;
        }
    }

    /// <summary>Where it is and how big, one number at a time - see <see cref="StrokeItem.X"/>. Setting a size here
    /// sets the FONT size, because that is what resizing a piece of text means.</summary>
    public double X
    {
        get => Bounds.X;
        set => Move(new Vector2(value - Bounds.X, 0));
    }

    public double Y
    {
        get => Bounds.Y;
        set => Move(new Vector2(0, value - Bounds.Y));
    }

    public double Width
    {
        get => Bounds.Width;
        set => Resize(new Rect(Bounds.X, Bounds.Y, Math.Max(1e-9, value), Bounds.Height));
    }

    public double Height
    {
        get => Bounds.Height;
        set => Resize(new Rect(Bounds.X, Bounds.Y, Bounds.Width, Math.Max(1e-9, value)));
    }

    /// <summary>What it says, cut short. A list of pieces of text where every row reads "Text" is a list of nothing.
    /// </summary>
    public string Title => string.IsNullOrWhiteSpace(Text)
        ? "Text"
        : Text.Length <= 24 ? Text : Text[..24] + "...";

    public Rect Bounds
    {
        get
        {
            // Measured at ONE to one, so a box does not depend on where the camera is - what it covers on the plane is a
            // fact about the drawing. What is drawn is shaped at the camera's size, which is a different question.
            var size = MeasureAt(FontSize);
            return new Rect(Origin.X, Origin.Y, size.Width, size.Height);
        }
    }

    public void Move(Vector2 worldDelta) => Origin += worldDelta;

    /// <summary>Resizing text sets its SIZE, not a scale on top of it: a box twice as tall is text twice as big, which is
    /// the same thing a designer means by dragging the corner of a label.</summary>
    public void Resize(Rect world)
    {
        if (world.Height <= 0) return;

        var was = MeasureAt(FontSize);
        if (was.Height <= 0) return;

        FontSize = Math.Max(0.01, FontSize * world.Height / was.Height);
        Origin = new Vector2(world.X, world.Y);
    }

    public bool HitTest(Vector2 world, double tolerance)
    {
        var box = Bounds;

        return world.X >= box.X - tolerance && world.X <= box.X + box.Width + tolerance &&
               world.Y >= box.Y - tolerance && world.Y <= box.Y + box.Height + tolerance;
    }

    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
        if (canvas == null || string.IsNullOrEmpty(Text) || Brush is not SolidColorBrush solid) return;

        // Shaped at the size it will be DRAWN at - see the note at the top. Below a pixel there is nothing to read, and
        // shaping it would be work thrown away.
        var onScreen = FontSize * canvas.Scale;
        if (onScreen < 1) return;

        var size = ShapeForDrawing(onScreen);
        if (size.Width <= 0 || size.Height <= 0) return;

        var at = canvas.WorldToScreen(Origin);

        session.DrawText(
            new TextRenderingParameters
            {
                HorizontalTextAlignment = HorizontalTextAlignment.Left,
                VerticalTextAlignment = VerticalTextAlignment.Top,
                TextTrimming = TextTrimming.None,
                TextWrapping = TextWrapping.NoWrap,
                Color = solid.Color,
                TextArea = new Rectangle(new Vector2F((float)at.X, (float)at.Y), size)
            },
            size, _draw, Brush, Brushes.Transparent, Brushes.Transparent);
    }

    // What the text COVERS at a given size, worked out on the measuring layout - never on the one being drawn.
    private Size MeasureAt(double fontSize)
    {
        if (!EnsureFont()) return default;
        if (_shapedText == Text && _shapedSize.Equals(fontSize)) return _measured;

        _measured = Shape(_measure, fontSize);
        _shapedText = Text;
        _shapedSize = fontSize;

        return _measured;
    }

    // ...and the layout that is actually handed to the drawing, shaped at the size it will appear on screen.
    private Size ShapeForDrawing(double fontSize)
    {
        if (!EnsureFont()) return default;
        if (_drawnText == Text && _drawnSize.Equals(fontSize)) return _drawn;

        _drawn = Shape(_draw, fontSize);
        _drawnText = Text;
        _drawnSize = fontSize;

        return _drawn;
    }

    private Size Shape(TextLayout layout, double fontSize) =>
        layout.ProcessText(Text, fontSize, new Size(double.NaN, double.NaN), TextWrapping.NoWrap,
            TextTrimming.None, HorizontalTextAlignment.Left, VerticalTextAlignment.Top, false);

    // A typeface is fixed per layout, so a different face is a different layout - the same rule TextBlock follows.
    private bool EnsureFont()
    {
        var font = FontFamily ?? UIComponent.DefaultFontFamily;
        if (font == null) return false;

        if (_draw != null && ReferenceEquals(_layoutFont, font)) return true;

        _draw = new TextLayout(font.Typeface, font.Fonts[0]);
        _measure = new TextLayout(font.Typeface, font.Fonts[0]);
        _layoutFont = font;
        _shapedText = null;
        _drawnText = null;

        return true;
    }
}
