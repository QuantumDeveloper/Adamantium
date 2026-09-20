using System.Runtime.InteropServices;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>WHAT AN IMPORTED CONTOUR ACTUALLY DRAWS. The mesh can hold the right holes and the screen still show a
/// solid block - the fill goes through a batch, a frozen snapshot and an AA fringe on the way, and only the pixels say
/// what came out.</summary>
[TestFixture]
[Category("Gpu")]
public class PathItemRenderTests
{
    private const int Dim = 200;

    private static OffscreenTestRenderer _renderer;

    [OneTimeSetUp]
    public void CreateRenderer()
    {
        var device = GpuTestDevice.Device;
        _renderer = new OffscreenTestRenderer(device, new RenderUnitFactory(device, new DeviceResourceFactory(device)),
            Dim, Dim)
        {
            ClearColor = Colors.Black
        };
    }

    [OneTimeTearDown]
    public void DisposeRenderer()
    {
        _renderer?.Dispose();
        _renderer = null;
    }

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

    // A square with a square hole, the way an exported icon states one: the hole runs the other way round and nothing
    // carries a Z.
    private const string Ring = "M -50 -50 L 50 -50 L 50 50 L -50 50 M -25 -25 L -25 25 L 25 25 L 25 -25";

    // The same question asked of a REAL exported icon: twenty-one sub-paths, none of them closed, the cells cut by
    // winding. A simple ring proves the machinery; this proves it survives the shape people actually import.
    private const string Grid =
        "M30.8623 12.6036L27.4694 6.15059L15.097 6C15.0367 6.00021 14.977 6.01368 14.9217 6.03959C14.8664 6.06549 14.81"
        + "67 6.10328 14.7756 6.15059L11.6055 9.76471H11.5747V9.79765L8.30328 13.5294H8.05231V20.1741L7 19.0494V20.4047L8"
        + ".4926 22L9.98521 20.4047V19.0494L8.9329 20.1741V14.4706H23.0224V20.1741L21.9701 19.0494V20.4047L23.4627 22L24."
        + "9553 20.4047V19.0494L23.903 20.1741V14.4706H24.3433L28.9294 9L29.9817 12.7953L28.9294 11.6706V13.0259L30.422 1"
        + "4.6212L29.4043 12.7949V11.6706L30.8623 12.6036ZM18.7559 13.5294L19.5793 12.5882H21.4549L20.6316 13.5294H18.755"
        + "9ZM15.6738 13.5294L16.4972 12.5882H18.3728L17.5495 13.5294H15.6738ZM12.5918 13.5294L13.4151 12.5882H15.2908L14"
        + ".4674 13.5294H12.5918ZM15.0662 10.7059H16.9419L16.1185 11.6471H14.2429L15.0662 10.7059ZM13.0365 11.6471H11.160"
        + "8L11.9841 10.7059H13.8598L13.0365 11.6471ZM20.2441 6.94118L19.4207 7.88235H17.5451L18.3684 6.94118H20.2441ZM23"
        + ".3262 6.94118L22.5028 7.88235H20.6272L21.4505 6.94118H23.3262ZM18.976 9.76471L19.7994 8.82353H21.6751L20.8517 "
        + "9.76471H18.976ZM20.024 10.7059L19.2006 11.6471H17.3249L18.1483 10.7059H20.024ZM18.593 8.82353L17.7696 9.76471H"
        + "15.894L16.7173 8.82353H18.593ZM20.407 11.6471L21.2304 10.7059H23.106L22.2827 11.6471H20.407ZM24.3124 10.7059H2"
        + "6.1881L25.3647 11.6471H23.4891L24.3124 10.7059ZM25.1402 9.76471L25.9635 8.82353H27.8392L26.985 9.79765V9.76471"
        + "H25.1402ZM23.9338 9.76471H22.0581L22.8815 8.82353H24.7571L23.9338 9.76471ZM23.7092 7.88235L24.5326 6.94118H26."
        + "4082L25.5849 7.88235H23.7092ZM15.2864 6.94118H17.162L16.3387 7.88235H14.463L15.2864 6.94118ZM13.6353 8.82353H1"
        + "5.5109L14.6876 9.76471H12.8119L13.6353 8.82353ZM10.333 12.5882H12.2087L11.3853 13.5294H9.50969L10.333 12.5882Z"
        + "M21.838 13.5294L22.6613 12.5882H24.537L23.7136 13.5294H21.838ZM28.5816 8.03294L26.7913 7.88235L27.6147 6.94118"
        + "L27.8392 7.73461L28.5816 8.03294Z";

    // OPEN, same reason as SvgPathParsingTests.TheCellsOfARealGridAreHoles: the non-zero rule is not applied to
    // contours that cross.
    [Test]
    public void TheCellsOfAnImportedGridAreHolesOnScreen()
    {
        var canvas = new InfiniteCanvas
        {
            Template = Template(),
            Scene = new CanvasScene(),
            Ink = Brushes.Black,
            Width = Dim,
            Height = Dim,
            GridStyle = CanvasGridStyle.Transparent
        };

        var path = new PathItem(Grid, new SolidColorBrush(Colors.Lime), null) { FillRule = FillRule.NonZero };

        // Where a hand actually has it: the item keeps the box the drawing states, and the CAMERA is what makes it
        // big. Resized instead, the contour is stretched by the item and the camera never enters into it - which is
        // not the case that goes wrong on the stand.
        path.Resize(new Rect(-12, -8, 23.8623, 16));
        canvas.Scene.Add(path);
        canvas.Scale = 8;

        for (var pass = 0; pass < 2; pass++)
        {
            canvas.Measure(new Size(Dim, Dim));
            canvas.Arrange(new Rect(0, 0, Dim, Dim));
        }

        var root = new VisualRoot(canvas, Dim, Dim);

        Assert.That(_renderer.RenderFrame(root), Is.True);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];

        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);

        // The cell at (20.1, 13.06) in the drawing's own units, carried into the box the item was put in.
        var source = new Rect(7, 6, 23.8623, 16);
        var cell = new Vector2(-12 + (20.1 - source.X), -8 + (13.06 - source.Y));

        var at = canvas.WorldToScreen(cell);
        var i = ((int)at.Y * Dim + (int)at.X) * 4;
        var colour = (R: pixels[i + 2], G: pixels[i + 1], B: pixels[i + 0]);

        Assert.That(colour.G, Is.LessThan(60), $"a cell of the grid came out filled: {colour}");
    }

    [Test]
    public void AHoleInAnImportedContourIsActuallyEmpty()
    {
        var canvas = new InfiniteCanvas
        {
            Template = Template(),
            Scene = new CanvasScene(),
            Ink = Brushes.Black,
            Width = Dim,
            Height = Dim,
            GridStyle = CanvasGridStyle.Transparent
        };

        var path = new PathItem(Ring, new SolidColorBrush(Colors.Lime), null) { FillRule = FillRule.NonZero };

        canvas.Scene.Add(path);

        for (var pass = 0; pass < 2; pass++)
        {
            canvas.Measure(new Size(Dim, Dim));
            canvas.Arrange(new Rect(0, 0, Dim, Dim));
        }

        var root = new VisualRoot(canvas, Dim, Dim);

        Assert.That(_renderer.RenderFrame(root), Is.True);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];

        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);

        (byte R, byte G, byte B) At(int x, int y)
        {
            var i = (y * Dim + x) * 4;
            return (pixels[i + 2], pixels[i + 1], pixels[i + 0]);
        }

        // The world's origin is the middle of the viewport, so the ring sits centred: its body is a quarter of the way
        // out, its hole is the middle.
        var body = At(Dim / 2, Dim / 2 - 35);
        var hole = At(Dim / 2, Dim / 2);

        Assert.Multiple(() =>
        {
            Assert.That(body.G, Is.GreaterThan(120), $"the contour itself was not drawn: {body}");
            Assert.That(hole.G, Is.LessThan(60), $"the hole came out filled: {hole}");
        });
    }
}
