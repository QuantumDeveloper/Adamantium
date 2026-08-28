using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Drawings;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UITests.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A DrawingImage is a picture with no pixels: it replays its shapes into whoever shows it. These assert the
/// replay - what is drawn, and WHERE - because that is the whole contract between the drawing and its consumer.</summary>
[TestFixture]
public class DrawingImageTests
{
    public class AccentSource
    {
        public Color Accent { get; set; }
    }

    private static GeometryDrawing Square(double x, double y, double size, Color color) =>
        new()
        {
            Geometry = new RectangleGeometry { Rect = new Rect(x, y, size, size) },
            Brush = new SolidColorBrush(color)
        };

    private static Vector2 Place(Matrix4x4F transform, double x, double y)
    {
        var p = Vector3F.TransformCoordinate(new Vector3F((float)x, (float)y, 0), transform);
        return new Vector2(p.X, p.Y);
    }

    [Test]
    public void Size_ComesFromTheDrawingsOwnExtent()
    {
        var image = new DrawingImage
        {
            Drawing = new DrawingGroup { Children = { Square(0, 0, 10, Colors.Red), Square(10, 0, 14, Colors.Blue) } }
        };

        Assert.That(image.Width, Is.EqualTo(24));
        Assert.That(image.Height, Is.EqualTo(14));
    }

    [Test]
    public void Replay_MapsTheViewboxOntoTheDestination()
    {
        // Authored in a 0..10 box, shown in a 100x100 slot at (50,50): the corners must land on the slot's corners.
        var image = new DrawingImage { Drawing = Square(0, 0, 10, Colors.Red) };
        var session = new RecordingDrawingSession();

        image.Render(session, new Rect(50, 50, 100, 100));

        Assert.That(session.Geometries, Has.Count.EqualTo(1));
        var transform = session.Geometries[0].Transform;
        Assert.That(Place(transform, 0, 0), Is.EqualTo(new Vector2(50, 50)));
        Assert.That(Place(transform, 10, 10), Is.EqualTo(new Vector2(150, 150)));
    }

    [Test]
    public void Replay_DoesNotCarryTheViewboxOffsetIntoTheDestination()
    {
        // Shapes starting at (8,8) - the leading gap belongs to the viewbox, not to the picture, so it must NOT survive
        // scaled up. The same trap a shape's own leading gap sets when it is measured from the origin.
        var image = new DrawingImage { Drawing = Square(8, 8, 4, Colors.Red) };
        var session = new RecordingDrawingSession();

        image.Render(session, new Rect(0, 0, 40, 40));

        var transform = session.Geometries[0].Transform;
        Assert.That(Place(transform, 8, 8), Is.EqualTo(new Vector2(0, 0)));
        Assert.That(Place(transform, 12, 12), Is.EqualTo(new Vector2(40, 40)));
    }

    [Test]
    public void OneGeometryAtTwoSizes_StaysOneGeometryWithTwoPlacements()
    {
        // The reason the replay carries a matrix at all: the SAME mesh has to serve every size. If the placement were
        // baked into the geometry, these would be two different shapes and the instancing would be gone.
        var shared = new RectangleGeometry { Rect = new Rect(0, 0, 10, 10) };
        var image = new DrawingImage
        {
            Drawing = new DrawingGroup
            {
                Children =
                {
                    new GeometryDrawing { Geometry = shared, Brush = new SolidColorBrush(Colors.Red) },
                    new GeometryDrawing { Geometry = shared, Brush = new SolidColorBrush(Colors.Blue) }
                }
            }
        };
        var session = new RecordingDrawingSession();

        image.Render(session, new Rect(0, 0, 20, 20));

        Assert.That(session.Geometries, Has.Count.EqualTo(2));
        Assert.That(session.Geometries[0].Geometry, Is.SameAs(session.Geometries[1].Geometry));
        Assert.That(session.Geometries[0].Geometry, Is.SameAs(shared));
    }

    [Test]
    public void GroupTransform_PlacesItsChildrenWithoutTouchingTheirGeometry()
    {
        var shared = new RectangleGeometry { Rect = new Rect(0, 0, 10, 10) };
        var moved = new DrawingGroup
        {
            Transform = new Transform { TranslateX = 10 },
            Children = { new GeometryDrawing { Geometry = shared, Brush = new SolidColorBrush(Colors.Red) } }
        };
        var image = new DrawingImage
        {
            Drawing = new DrawingGroup
            {
                Children = { new GeometryDrawing { Geometry = shared, Brush = new SolidColorBrush(Colors.Blue) }, moved }
            }
        };
        var session = new RecordingDrawingSession();

        // Viewbox is 0..20 wide; drawn 1:1 so the group's 10px shift stays 10px.
        image.Render(session, new Rect(0, 0, 20, 10));

        Assert.That(session.Geometries, Has.Count.EqualTo(2));
        Assert.That(session.Geometries[0].Geometry, Is.SameAs(session.Geometries[1].Geometry));
        Assert.That(Place(session.Geometries[0].Transform, 0, 0).X, Is.EqualTo(0).Within(1e-6));
        Assert.That(Place(session.Geometries[1].Transform, 0, 0).X, Is.EqualTo(10).Within(1e-6));
    }

    /// <summary>A group whose content stays inside its siblings' box while it turns must not grow the picture. Merging
    /// the children's boxes and rotating THAT inflates it - the union of axis-aligned boxes is bigger than the shapes in
    /// it, and its AABB under rotation is bigger again - so the viewbox grew with the angle and everything on the stand
    /// shrank and sprang back as the slider went round.</summary>
    [Test]
    public void RotatingAGroupInsideItsBox_DoesNotGrowThePicture()
    {
        var spinner = new DrawingGroup
        {
            Transform = new Transform { RotationCenterX = 12, RotationCenterY = 12 },
            Children =
            {
                new GeometryDrawing { Geometry = new RectangleGeometry { Rect = new Rect(10.5, 3, 3, 18) }, Brush = new SolidColorBrush(Colors.Red) },
                new GeometryDrawing { Geometry = new RectangleGeometry { Rect = new Rect(3, 10.5, 18, 3) }, Brush = new SolidColorBrush(Colors.Blue) }
            }
        };
        var image = new DrawingImage
        {
            Drawing = new DrawingGroup
            {
                Children =
                {
                    new GeometryDrawing { Geometry = new RectangleGeometry { Rect = new Rect(0, 0, 24, 24) }, Brush = new SolidColorBrush(Colors.Black) },
                    spinner
                }
            }
        };

        for (var angle = 0; angle <= 360; angle += 15)
        {
            spinner.Transform.RotationAngle = angle;

            Assert.That(image.Width, Is.EqualTo(24).Within(1e-6), $"width at {angle} deg");
            Assert.That(image.Height, Is.EqualTo(24).Within(1e-6), $"height at {angle} deg");
        }
    }

    [Test]
    public void RecolouringAShapeDeepInside_ReachesTheImage()
    {
        var brush = new SolidColorBrush(Colors.Red);
        var image = new DrawingImage
        {
            Drawing = new DrawingGroup
            {
                Children =
                {
                    new DrawingGroup
                    {
                        Children = { new GeometryDrawing { Geometry = new RectangleGeometry { Rect = new Rect(0, 0, 10, 10) }, Brush = brush } }
                    }
                }
            }
        };

        var raised = 0;
        image.Changed += (_, _) => raised++;

        brush.Color = Colors.Blue;

        Assert.That(raised, Is.GreaterThan(0));
    }

    /// <summary>The same, for a STROKED shape. Half an icon set is strokes - a cross, a checkmark, an arrow - and the
    /// stroke brush was the one half of the pair nothing watched: only <c>Brush</c> was hooked, so recolouring a stroke
    /// changed the picture and told nobody. Whoever holds PIXELS of the drawing (a baked tile brush, a nine-slice) then
    /// keeps the old ones for good, because the bake is only ever thrown away on this event.</summary>
    [Test]
    public void RecolouringASTROKE_ReachesTheImage()
    {
        var brush = new SolidColorBrush(Colors.Red);
        var image = new DrawingImage
        {
            Drawing = new GeometryDrawing
            {
                Geometry = new RectangleGeometry { Rect = new Rect(0, 0, 10, 10) },
                Stroke = brush,
                StrokeThickness = 2
            }
        };

        var raised = 0;
        image.Changed += (_, _) => raised++;

        brush.Color = Colors.Blue;

        Assert.That(raised, Is.GreaterThan(0));
    }

    /// <summary>A drawing lives in a RESOURCE, outside the tree. Attaching it to the element that shows it is the ONLY
    /// route a binding inside it has to a DataContext - without it every such binding yields null, which for a brush
    /// means the shape draws nothing at all and says nothing about why.</summary>
    [Test]
    public void BindingInsideADrawing_ResolvesAgainstTheOwnersDataContext()
    {
        var brush = new SolidColorBrush();   // no ctor value: a local one would OUTRANK the binding and mask it
        brush.SetBinding(SolidColorBrush.ColorProperty, new Binding("Accent"));

        var image = new DrawingImage
        {
            Drawing = new DrawingGroup
            {
                Children = { new GeometryDrawing { Geometry = new RectangleGeometry { Rect = new Rect(0, 0, 10, 10) }, Brush = brush } }
            }
        };

        var owner = new Image { DataContext = new AccentSource { Accent = Colors.Red } };
        image.Attach(owner);

        Assert.That(brush.Color, Is.EqualTo(Colors.Red));
    }

    /// <summary>Text in a drawing has to be SHAPED, not merely laid out. The short ProcessText overload returns a correct
    /// size while leaving the layout with no font size, no atlas and no glyphs - so the run measured right, placed right
    /// and drew absolutely nothing. Only the long overload records those, and the glyph snapshot the renderer freezes is
    /// taken off them.</summary>
    [Test]
    public void TextInADrawing_IsShaped_NotJustMeasured()
    {
        var run = new GlyphRunDrawing { Text = "Ag", FontSize = 12, Foreground = new SolidColorBrush(Colors.White) };
        var image = new DrawingImage { Drawing = run };
        var session = new RecordingDrawingSession();

        image.Render(session, new Rect(0, 0, image.Width, image.Height));

        Assert.That(session.Texts, Has.Count.EqualTo(1));
        var layout = session.Texts[0].Layout;
        // FontSize is the discriminator: the short overload never records it, and the atlas the glyph snapshot needs is
        // built off it. The glyph COUNT cannot be asserted here - the items are filled in against a graphics device.
        Assert.That(layout.FontSize, Is.GreaterThan(0), "font size never recorded - the layout was not shaped");
    }

    /// <summary>Two elements showing the SAME drawing at different sizes must not share one layout: each render unit
    /// freezes the glyphs off the layout it was handed, so a shared one leaves every consumer but the last drawing
    /// whatever the last shaping put there.</summary>
    [Test]
    public void OneRunAtTwoSizes_GetsItsOwnShaping()
    {
        var image = new DrawingImage
        {
            Drawing = new GlyphRunDrawing { Text = "Ag", FontSize = 12, Foreground = new SolidColorBrush(Colors.White) }
        };
        var session = new RecordingDrawingSession();

        image.Render(session, new Rect(0, 0, image.Width, image.Height));
        image.Render(session, new Rect(0, 0, image.Width * 4, image.Height * 4));

        Assert.That(session.Texts, Has.Count.EqualTo(2));
        Assert.That(session.Texts[0].Layout, Is.Not.SameAs(session.Texts[1].Layout));
        Assert.That(session.Texts[0].Layout.FontSize, Is.LessThan(session.Texts[1].Layout.FontSize));
    }

    /// <summary>A glyph run is anchored by its CORNER, so a fixed Origin centres exactly one string and drifts as soon as
    /// the text changes length. The box is what makes centring hold: the same box with a longer caption must stay centred,
    /// and the drawing's extent must not move with the words either.</summary>
    [Test]
    public void TextCentredInABox_StaysCentredWhenTheStringChanges()
    {
        var run = new GlyphRunDrawing
        {
            Text = "Ag",
            FontSize = 10,
            Box = new Rect(0, 0, 100, 40),
            Foreground = new SolidColorBrush(Colors.White)
        };
        var image = new DrawingImage { Drawing = run };

        var session = new RecordingDrawingSession();
        image.Render(session, new Rect(0, 0, 200, 80));   // drawn at 2x

        // The alignment must reach the LAYOUT, shaping into the box: the ink does not sit centred inside a line box
        // (ascender and descender space are not symmetric), so centring from out here lands the text visibly low.
        var shaped = session.Texts[0].Layout.RenderingParameters;
        Assert.That(shaped.HorizontalTextAlignment, Is.EqualTo(HorizontalTextAlignment.Center));
        Assert.That(shaped.VerticalTextAlignment, Is.EqualTo(VerticalTextAlignment.Center));
        Assert.That(shaped.TextArea.Width, Is.EqualTo(200).Within(0.5), "the box, scaled, is what the run aligns in");
        Assert.That(shaped.TextArea.Height, Is.EqualTo(80).Within(0.5));

        // And the extent must not follow the words - otherwise a longer caption resizes the whole icon.
        run.Text = "A much longer caption";
        Assert.That(image.Width, Is.EqualTo(100));
        Assert.That(image.Height, Is.EqualTo(40));
    }

    [Test]
    public void AddingAShapeToAGroup_ReachesTheImage()
    {
        var group = new DrawingGroup();
        var image = new DrawingImage { Drawing = group };

        var raised = 0;
        image.Changed += (_, _) => raised++;

        group.Children.Add(Square(0, 0, 10, Colors.Red));

        Assert.That(raised, Is.GreaterThan(0));
    }
}
