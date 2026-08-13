using System.Collections.Generic;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Media.Drawings;

/// <summary>Text inside a drawing - a labelled diagram, a lettered badge. The text is RE-SHAPED at the size it is drawn
/// at rather than scaled as a picture, so an icon blown up to 512px gets glyphs shaped for 512px, not stretched ones.</summary>
public class GlyphRunDrawing : Drawing
{
    // ONE layout per size, not one per drawing. A drawing is a shared RESOURCE: the same run is shown by several elements
    // at once, each shaping at its own size, and each render unit snapshots the glyphs off the layout it was given. With a
    // single layout they all re-shape the SAME object and only the last shaping survives - so every other consumer draws
    // whatever that left behind. Same size = same glyphs, so sharing within a size is safe.
    private readonly Dictionary<double, TextLayout> _layouts = [];
    private FontFamily _layoutFont;

    public static readonly AdamantiumProperty TextProperty = AdamantiumProperty.Register(nameof(Text),
        typeof(string), typeof(GlyphRunDrawing), new PropertyMetadata(string.Empty));

    public static readonly AdamantiumProperty FontFamilyProperty = AdamantiumProperty.Register(nameof(FontFamily),
        typeof(FontFamily), typeof(GlyphRunDrawing), new PropertyMetadata(null));

    public static readonly AdamantiumProperty FontSizeProperty = AdamantiumProperty.Register(nameof(FontSize),
        typeof(double), typeof(GlyphRunDrawing), new PropertyMetadata(12.0));

    public static readonly AdamantiumProperty ForegroundProperty = AdamantiumProperty.Register(nameof(Foreground),
        typeof(Brush), typeof(GlyphRunDrawing), new PropertyMetadata(null, ForegroundChangedCallback));

    public static readonly AdamantiumProperty OriginProperty = AdamantiumProperty.Register(nameof(Origin),
        typeof(Vector2), typeof(GlyphRunDrawing), new PropertyMetadata(default(Vector2)));

    public static readonly AdamantiumProperty BoxProperty = AdamantiumProperty.Register(nameof(Box),
        typeof(Rect), typeof(GlyphRunDrawing), new PropertyMetadata(default(Rect)));

    public static readonly AdamantiumProperty HorizontalAlignmentProperty = AdamantiumProperty.Register(nameof(HorizontalAlignment),
        typeof(HorizontalTextAlignment), typeof(GlyphRunDrawing), new PropertyMetadata(HorizontalTextAlignment.Center));

    public static readonly AdamantiumProperty VerticalAlignmentProperty = AdamantiumProperty.Register(nameof(VerticalAlignment),
        typeof(VerticalTextAlignment), typeof(GlyphRunDrawing), new PropertyMetadata(VerticalTextAlignment.Center));

    public string Text
    {
        get => GetValue<string>(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The typeface. Null falls back to the theme's default font, exactly as an unset FontFamily on an element does.</summary>
    public FontFamily FontFamily
    {
        get => GetValue<FontFamily>(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Size in the DRAWING's own units - it is scaled along with everything else when the drawing is shown.</summary>
    public double FontSize
    {
        get => GetValue<double>(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public Brush Foreground
    {
        get => GetValue<Brush>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>The run's top-left corner in the drawing's own coordinates. Ignored once <see cref="Box"/> is set.</summary>
    public Vector2 Origin
    {
        get => GetValue<Vector2>(OriginProperty);
        set => SetValue(OriginProperty, value);
    }

    /// <summary>A box in the drawing's own coordinates to ALIGN the run inside, instead of pinning its corner with
    /// <see cref="Origin"/>. This is the only way to centre a run: a glyph run is anchored by its corner, so a fixed
    /// origin is exact for exactly one string and drifts the moment the text changes length. Empty (the default) keeps
    /// the origin behaviour. The box also becomes the run's <see cref="Bounds"/>, so a drawing built around one keeps
    /// the same extent whatever the text says.</summary>
    public Rect Box
    {
        get => GetValue<Rect>(BoxProperty);
        set => SetValue(BoxProperty, value);
    }

    /// <summary>Where the run sits horizontally in <see cref="Box"/>. Centre by default - aligning is the reason to
    /// declare a box at all.</summary>
    public HorizontalTextAlignment HorizontalAlignment
    {
        get => GetValue<HorizontalTextAlignment>(HorizontalAlignmentProperty);
        set => SetValue(HorizontalAlignmentProperty, value);
    }

    /// <summary>Where the run sits vertically in <see cref="Box"/>.</summary>
    public VerticalTextAlignment VerticalAlignment
    {
        get => GetValue<VerticalTextAlignment>(VerticalAlignmentProperty);
        set => SetValue(VerticalAlignmentProperty, value);
    }

    private static void ForegroundChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not GlyphRunDrawing drawing) return;

        if (e.OldValue is Brush oldBrush)
        {
            oldBrush.Changed -= drawing.OnChildChanged;
        }

        if (e.NewValue is Brush newBrush)
        {
            newBrush.Changed += drawing.OnChildChanged;
        }
    }

    protected override void AttachChildren() => AttachOwned(Foreground);

    // The typeface is fixed per TextLayout, so a font change means new ones (same rule TextBlock follows).
    private TextLayout EnsureLayout(double fontSize)
    {
        var font = FontFamily ?? Theme.SystemDefaultFontFamily;
        if (!ReferenceEquals(_layoutFont, font))
        {
            _layouts.Clear();
            _layoutFont = font;
        }

        if (!_layouts.TryGetValue(fontSize, out var layout))
        {
            layout = new TextLayout(font.Typeface, font.Fonts[0]);
            _layouts[fontSize] = layout;
        }

        return layout;
    }

    /// <summary>Shape the run and return its natural size. It MUST go through this overload: the short one only lays the
    /// text out, while this is what records the layout's Text/FontSize and marks it dirty - which is what later builds
    /// the glyph atlas. Called directly, the short one returns a correct size and leaves the layout with no atlas and no
    /// glyphs at all, so the run measured right and drew nothing. NaN = no wrap boundary (the overload maps it).</summary>
    private static Size Shape(TextLayout layout, string text, double fontSize, Size area,
        HorizontalTextAlignment horizontal, VerticalTextAlignment vertical) =>
        layout.ProcessText(text, fontSize, area, TextWrapping.NoWrap, TextTrimming.None, horizontal, vertical);

    // Natural size, no box: NaN = no wrap boundary (the overload maps it).
    private static Size Shape(TextLayout layout, string text, double fontSize) =>
        Shape(layout, text, fontSize, new Size(double.NaN, double.NaN), HorizontalTextAlignment.Left,
            VerticalTextAlignment.Top);

    private static TextRenderingParameters Parameters(Brush foreground, Rect area,
        HorizontalTextAlignment horizontal = HorizontalTextAlignment.Left,
        VerticalTextAlignment vertical = VerticalTextAlignment.Top) =>
        new()
        {
            HorizontalTextAlignment = horizontal,
            VerticalTextAlignment = vertical,
            TextTrimming = TextTrimming.None,
            TextWrapping = TextWrapping.NoWrap,
            Color = (foreground as SolidColorBrush)?.Color ?? Colors.White,
            TextArea = new Rectangle(new Vector2F((float)area.X, (float)area.Y),
                new Size(area.Width, area.Height))
        };

    public override Rect Bounds
    {
        get
        {
            var text = Text;
            if (string.IsNullOrEmpty(text)) return Rect.Empty;

            var box = Box;
            if (!box.IsEmpty) return box;

            var origin = Origin;
            var size = Shape(EnsureLayout(FontSize), text, FontSize);

            return new Rect(origin.X, origin.Y, size.Width, size.Height);
        }
    }

    public override void Render(IDrawingSession session, Matrix4x4F transform)
    {
        var text = Text;
        if (string.IsNullOrEmpty(text)) return;

        // The SIZE goes into the font, not into a scale on an already-shaped run: re-shaping at the final size is what
        // keeps the glyphs crisp, which scaling a finished run could not do. A NON-UNIFORM scale has no single font size
        // to pick, so the smaller axis wins and such a drawing renders its text un-squashed rather than wrongly.
        var scale = System.Math.Min(TransformScale(transform, 0), TransformScale(transform, 1));
        if (scale <= 0) return;

        var fontSize = FontSize * scale;
        var layout = EnsureLayout(fontSize);

        var box = Box;
        Size desired;
        Vector2 corner;

        if (box.IsEmpty)
        {
            desired = Shape(layout, text, fontSize);
            corner = Origin;
        }
        else
        {
            // Shape INTO the box and let the LAYOUT align the run in it. Doing that arithmetic out here means centring on
            // the line box, and the ink does not sit centred inside a line box - ascender and descender space are not
            // symmetric - so the text came out visibly low. The layout is the only thing that knows those metrics.
            desired = new Size(box.Width * scale, box.Height * scale);
            Shape(layout, text, fontSize, desired, HorizontalAlignment, VerticalAlignment);
            corner = new Vector2(box.X, box.Y);
        }

        // The quad's TOP-LEFT. Measured, not assumed: placing it by the centre put the text half its own width and
        // height further down and right, which is what a top-left anchor does with a centre handed to it.
        var placed = new Rect(corner.X, corner.Y, 0, 0).TransformToAABB(transform);
        var place = Matrix4x4F.Translation((float)placed.X, (float)placed.Y, 0);
        var parameters = Parameters(Foreground, new Rect(0, 0, desired.Width, desired.Height),
            HorizontalAlignment, VerticalAlignment);

        session.DrawText(parameters, desired, layout, Foreground, Brushes.Transparent, Brushes.Transparent, place);
    }

    // Length of the transformed unit axis - the scale actually applied along it, rotation included.
    private static double TransformScale(Matrix4x4F transform, int axis)
    {
        var unit = axis == 0 ? new Vector3F(1, 0, 0) : new Vector3F(0, 1, 0);
        var origin = Vector3F.TransformCoordinate(Vector3F.Zero, transform);
        var tip = Vector3F.TransformCoordinate(unit, transform);
        return (tip - origin).Length();
    }
}
