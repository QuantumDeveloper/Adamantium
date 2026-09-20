using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A CONTROL ON THE PLANE UNDER THE CAMERA. It is a real child of a layer, so the camera reaches it as a
/// transform on a finished control rather than as a new layout - and the question every zoom asks is whether the thing
/// still stands where its frame is drawn and at the size that frame has.</summary>
public class CanvasZoomHostTests
{
    private static ControlTemplate Template() => new(() =>
    {
        var layers = new Grid();
        var front = new CanvasFrontLayer();
        var root = new Grid();

        root.Children.Add(layers);
        root.Children.Add(front);

        var result = new TemplateResult { RootComponent = root };

        result.RegisterName("PART_Layers", layers);
        result.RegisterName("PART_Front", front);
        return result;
    });

    private static (InfiniteCanvas Canvas, ElementItem Item, Button Control) Stage()
    {
        var control = new Button { Width = 100, Height = 50 };
        var item = new ElementItem(control, new Rect(20, 10, 100, 50));
        var canvas = new InfiniteCanvas
        {
            Template = Template(),
            Scene = new CanvasScene(),
            Width = 800,
            Height = 600
        };

        canvas.Scene.Add(item);
        Lay(canvas);

        return (canvas, item, control);
    }

    // A pass the way the application runs one: the layers are invalidated by the camera, and only the manager they are
    // enqueued into settles them - a Measure/Arrange on the canvas alone stops at whatever is already valid.
    private static void Lay(InfiniteCanvas canvas)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            canvas.Measure(new Size(800, 600), force: true);
            canvas.Arrange(new Rect(0, 0, 800, 600));
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(canvas);
        }
    }

    [TestCase(1.0)]
    [TestCase(4.0)]
    [TestCase(0.25)]
    [TestCase(16.0)]
    [TestCase(0.05)]
    public void AHostedControlStandsWhereTheCameraPutsIt(double scale)
    {
        var (canvas, item, control) = Stage();

        canvas.Scale = scale;
        canvas.Offset = new Vector2(400, 300);
        Lay(canvas);

        var corner = canvas.WorldToScreen(new Vector2(item.World.X, item.World.Y));
        var turn = ((IUIComponent)control).RenderTransform;

        Assert.Multiple(() =>
        {
            Assert.That(control.Bounds.X, Is.EqualTo(corner.X).Within(0.5), "it is not where its frame is drawn");
            Assert.That(control.Bounds.Y, Is.EqualTo(corner.Y).Within(0.5), "it is not where its frame is drawn");

            // Measured in WORLD units and scaled by the camera: what is seen is the two multiplied, and that is what
            // has to match the frame.
            Assert.That(control.RenderSize.Width, Is.EqualTo(item.World.Width).Within(0.5), "it was laid out at the wrong size");
            Assert.That(control.RenderSize.Height, Is.EqualTo(item.World.Height).Within(0.5), "it was laid out at the wrong size");
            Assert.That(turn?.ScaleX ?? 1, Is.EqualTo(scale).Within(0.001), "the camera's scale did not reach it");
            Assert.That(turn?.ScaleY ?? 1, Is.EqualTo(scale).Within(0.001), "the camera's scale did not reach it");
        });
    }

    // A CONTROL PUT DOWN BY A CLICK IS THE SAME SIZE WHATEVER THE CAMERA IS AT. Taken in screen pixels, the natural
    // size a click gives it grew with the zoom - at 0.36x a checkbox landed nearly three times its own size in the
    // world, while the box and label inside it stayed what they always are: a small control in the corner of a large
    // empty frame, and no way to get it back short of resizing it by hand.
    [TestCase(1.0)]
    [TestCase(0.36)]
    [TestCase(4.0)]
    public void AControlPutDownByAClickIsTheSameSizeAtAnyZoom(double scale)
    {
        var canvas = new InfiniteCanvas
        {
            Template = Template(),
            Scene = new CanvasScene(),
            Width = 800,
            Height = 600
        };

        Lay(canvas);
        canvas.Scale = scale;
        canvas.Offset = new Vector2(400, 300);
        Lay(canvas);

        var tool = new ElementTool(() => new Button(), new Size(140, 32));
        var where = new CanvasPointerEventArgs
        {
            World = new Vector2(0, 0),
            Pointer = new Vector2(0, 0),
            Screen = canvas.WorldToScreen(new Vector2(0, 0)),
            Button = MouseButtons.Left,
            ClickCount = 1
        };

        canvas.Tool = tool;
        tool.OnPressed(canvas, where);
        tool.OnReleased(canvas, where);

        var put = canvas.Selection[0] as ElementItem;

        Assert.That(put, Is.Not.Null, "the tool put nothing down");
        Assert.Multiple(() =>
        {
            Assert.That(put.World.Width, Is.EqualTo(140).Within(0.5), $"at {scale}x it landed a different width");
            Assert.That(put.World.Height, Is.EqualTo(32).Within(0.5), $"at {scale}x it landed a different height");
        });
    }

    // A SCALE WRITTEN STRAIGHT ZOOMS WHERE YOU ARE LOOKING. Scale alone leaves the world's ORIGIN where it is on
    // screen and swings everything else round it - a number typed into the panel threw the drawing off sideways while
    // the empty middle of the plane filled the window. The wheel never showed it: it goes through ZoomAt, which holds
    // the point under the cursor still.
    [TestCase(8)]
    [TestCase(0.2)]
    public void AScaleWrittenStraightKeepsWhatIsInTheMiddle(double scale)
    {
        var canvas = new InfiniteCanvas
        {
            Template = Template(),
            Scene = new CanvasScene(),
            Width = 800,
            Height = 600
        };

        Lay(canvas);

        // Looking somewhere well away from the world's origin, which is where the drift showed.
        canvas.Offset = new Vector2(-1200, -900);
        Lay(canvas);

        var room = canvas.UsableBounds;
        var middle = new Vector2(room.X + room.Width / 2, room.Y + room.Height / 2);
        var was = canvas.ScreenToWorld(middle);

        canvas.Scale = scale;
        Lay(canvas);

        var now = canvas.ScreenToWorld(middle);

        Assert.Multiple(() =>
        {
            Assert.That(now.X, Is.EqualTo(was.X).Within(0.5), "the camera slid sideways");
            Assert.That(now.Y, Is.EqualTo(was.Y).Within(0.5), "the camera slid up or down");
        });
    }

    // ...AND STAYS PUT WHEN THE CAMERA MOVES AGAIN. A zoom in followed by a zoom out must land it back where it was:
    // anything the layer remembers from the last camera - a size measured on screen, a transform applied twice - shows
    // up here as a control that grew or shrank on the way.
    [Test]
    public void ZoomingInAndOutAgainLeavesItAsItWas()
    {
        var (canvas, _, control) = Stage();

        canvas.Offset = new Vector2(400, 300);
        Lay(canvas);

        var was = control.RenderSize;

        canvas.Scale = 12;
        Lay(canvas);
        canvas.Scale = 0.08;
        Lay(canvas);
        canvas.Scale = 1;
        Lay(canvas);

        Assert.Multiple(() =>
        {
            Assert.That(control.RenderSize.Width, Is.EqualTo(was.Width).Within(0.5), "it came back a different width");
            Assert.That(control.RenderSize.Height, Is.EqualTo(was.Height).Within(0.5), "it came back a different height");
        });
    }
}
