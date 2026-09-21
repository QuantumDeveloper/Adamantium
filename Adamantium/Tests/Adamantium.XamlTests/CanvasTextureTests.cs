using System.Linq;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>A TEXTURE ON THE PLANE. Not a control of its own and not a picture of its own: a surface whose BACKGROUND
/// is a picture - so its box, its corners, its outline and its turn are the ones every control on the plane already
/// has, and the file is chosen on the same Source row a colour would be chosen on.</summary>
[TestFixture]
public class CanvasTextureTests
{
    private FakeApp _app;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    private void Use(Theme theme)
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);

        var themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = themes;
        ((FakeContext)_app.UIContext).ThemeEngine = themes;

        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);
    }

    private static (Window Window, InfiniteCanvas Canvas) Stage()
    {
        var canvas = new InfiniteCanvas { Scene = new CanvasScene() };

        canvas.ApplyCurrentTheme();

        var window = new Window { Width = 1000, Height = 700, Content = canvas };

        Settle(window);

        return (window, canvas);
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 6; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }
    }

    private static ElementItem Placed(InfiniteCanvas canvas, Rect box)
    {
        var tool = new TextureTool();

        canvas.Tool = tool;

        tool.OnPressed(canvas, Pointer(new Vector2(box.X, box.Y)));
        tool.OnMoved(canvas, Pointer(new Vector2(box.X + box.Width, box.Y + box.Height)));
        tool.OnReleased(canvas, Pointer(new Vector2(box.X + box.Width, box.Y + box.Height)));

        return canvas.ItemsHere().OfType<ElementItem>().Last();
    }

    private static CanvasPointerEventArgs Pointer(Vector2 at) => new()
    {
        World = at,
        Pointer = at,
        Button = Adamantium.UI.Core.Input.MouseButtons.Left,
        ClickCount = 1
    };

    // PLACED WITH A FILL AND NO QUESTION. A dialog in the middle of the gesture takes the pointer away, and what a
    // person has just dragged out would have to be dragged out again; what they get instead is a surface they can see
    // and take hold of, and the file is answered later in the panel.
    [Test]
    public void TheToolPlacesASurfaceWithAPlainBackgroundAndAsksNothing()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var (_, canvas) = Stage();
        var item = Placed(canvas, new Rect(20, 20, 200, 150));

        Assert.Multiple(() =>
        {
            Assert.That(item.Painted, Is.InstanceOf<Image>(), "a texture is a picture, even before it has a file");
            Assert.That((item.Painted as Image)?.Background, Is.Not.Null,
                "a texture was placed with nothing to see");

            // ...AND THE GROUND STAYS when a file is named. An Image keeps nothing in Source here - the picture is
            // painted ON the ground - so a ground that got out of the way for the picture would leave the surface
            // blank: a selection frame around nothing, which is exactly what was reported.
            Assert.That((item.Painted as Image)?.BackgroundState, Is.EqualTo(ImageBackgroundState.Always),
                "the ground the picture is painted on gets out of the way when one arrives");
            Assert.That(canvas.Selection, Has.Count.EqualTo(1), "what was placed was not taken hold of");
        });
    }

    // THE PICTURE IS A BACKGROUND, which is the whole design: the row that takes a file is the same row a colour goes
    // on, and everything already said about what carries it goes on meaning what it meant.
    [Test]
    public void TheSourceRowPaintsTheBackgroundWithThePicture()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var (window, canvas) = Stage();
        var item = Placed(canvas, new Rect(20, 20, 200, 150));

        Settle(window);

        // NOTHING UNFOLDED FIRST. A line that has to be opened to be found is a line nobody finds, and the picture is
        // the whole point of the object selected.
        var row = Row(canvas, "Source");

        Assert.That(row, Is.Not.Null, "the panel offers a selected texture no source");

        Assert.That(row.Owner.Write(row, "C:/pictures/leaf.png"), Is.True, "the panel refused the file");

        var picture = item.Tiled;

        Assert.Multiple(() =>
        {
            Assert.That(picture?.Source, Is.Not.Null, "the file did not reach the picture");
            Assert.That((picture?.Source as BitmapImage)?.UriSource?.OriginalString,
                Is.EqualTo("C:/pictures/leaf.png"), "it shows another file than the one chosen");
        });

        // ...AND A COLOUR WRITTEN AFTERWARDS LEAVES IT ALONE. The two are different things about the element - what it
        // shows, and what is behind what it shows - and they were one property until this: a colour picked in the
        // panel wiped the picture outright.
        // The colour lives in the Look block, which starts folded.
        Open(canvas, window);

        var ground = Row(canvas, "Background");

        Assert.That(ground, Is.Not.Null, "the panel offers no background colour");

        ground.Owner.Write(ground, Colors.Tomato);

        Assert.Multiple(() =>
        {
            Assert.That(item.Tiled?.Source, Is.Not.Null, "a colour wiped the picture");
            Assert.That((item.Tiled?.Background as SolidColorBrush)?.Color, Is.EqualTo(Colors.Tomato),
                "the colour did not reach the ground");
        });

        Settle(window);

        // ...AND THE ROW SAYS SO IN ITS TIP: a file two folders deep does not fit the cell, and which file this is is
        // the one question the row asks.
        Assert.That(row.ToolTip, Is.EqualTo("C:/pictures/leaf.png"), "the path is not repeated in the tip");
    }

    // A FILE CHOSEN BY POINTING is written exactly as a typed one: through the grid, so the edit is reported once and
    // can be taken back like any other.
    [Test]
    public void APickedFileGoesThroughTheSameWriteATypedOneDoes()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var (window, canvas) = Stage();
        var item = Placed(canvas, new Rect(20, 20, 200, 150));

        Settle(window);

        var row = Row(canvas, "Source");
        var reported = 0;

        row.Owner.ValueChanged += (_, _) => reported++;

        ((ImageSourceProperty)row.Definition).Pick("C:/pictures/bark.png");

        Assert.Multiple(() =>
        {
            Assert.That(item.Tiled?.Source, Is.Not.Null, "the chosen file never reached the picture");
            Assert.That(reported, Is.EqualTo(1), "the edit was not reported the way a typed one is");
        });
    }

    // ITS CORNERS ARE ROUNDED like a button's - asked of the surface BY NAME, the way a binding asks, so the next
    // thing that rounds its corners needs nothing added here to be reachable.
    [Test]
    public void TheCornerRowsRoundTheSurface()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var (_, canvas) = Stage();
        var item = Placed(canvas, new Rect(20, 20, 200, 150));

        item.CornerTopLeft = 12;
        item.CornerBottomRight = 4;

        var surface = (Image)item.Painted;

        Assert.Multiple(() =>
        {
            Assert.That(surface.CornerRadius.TopLeft, Is.EqualTo(12).Within(1e-9),
                "the corner never reached the surface");
            Assert.That(surface.CornerRadius.BottomRight, Is.EqualTo(4).Within(1e-9));
            Assert.That(item.CornerTopLeft, Is.EqualTo(12).Within(1e-9), "the row does not read back what it wrote");
        });
    }

    // IT HAS TO BE THERE TO SEE. A surface is placed to be looked at and taken hold of, so it fills the box the hand
    // dragged out - the selection frame is drawn by the canvas from the item's own rectangle, so a surface that came
    // out at nothing would leave exactly a frame around nothing.
    [Test]
    public void TheSurfaceFillsTheBoxItWasDraggedOut()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var (window, canvas) = Stage();
        var item = Placed(canvas, new Rect(20, 20, 200, 150));

        Settle(window);

        var surface = item.Painted;

        Assert.That(surface, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(surface.RenderSize.Width, Is.GreaterThan(0), "the surface takes no width at all");
            Assert.That(surface.RenderSize.Height, Is.GreaterThan(0), "the surface takes no height at all");
        });
    }

    // EVERYTHING A TEXTURE CAN DO. The engine can fit, repeat, mirror, turn and tint a picture, and all of it is the
    // brush's own - so the panel reaches it straight through the brush and the plane is no poorer a place to use a
    // texture than a window is.
    [Test]
    public void TheTextureRowsReachTheBrushItIsPaintedWith()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var (window, canvas) = Stage();
        var item = Placed(canvas, new Rect(20, 20, 200, 150));

        Settle(window);

        Row(canvas, "Source").Owner.Write(Row(canvas, "Source"), "C:/pictures/bark.png");

        Settle(window);

        Open(canvas, window);

        var fill = Row(canvas, "Fill");
        var repeat = Row(canvas, "Repeat");

        Assert.That(fill, Is.Not.Null, "no line says how the picture is fitted");
        Assert.That(repeat, Is.Not.Null, "no line says whether it repeats");

        // ...AND WHEN THE GROUND BEHIND IT SHOWS, which is the one thing about a picture the panel could not say.
        var ground = Row(canvas, "Ground");

        Assert.That(ground, Is.Not.Null, "no line says when the ground behind the picture is painted");

        ground.Owner.Write(ground, ImageBackgroundState.Never);

        Assert.That(item.Tiled.BackgroundState, Is.EqualTo(ImageBackgroundState.Never),
            "the ground's own line does not reach it");

        // ...AND THE PICTURE CAN BE PUT ASIDE without losing it: the file stays, it is simply not painted.
        var visible = Row(canvas, "Show picture");

        Assert.That(visible, Is.Not.Null, "no line puts the picture aside");

        visible.Owner.Write(visible, false);

        Assert.Multiple(() =>
        {
            Assert.That(item.Tiled.ShowsForeground, Is.False, "the switch does not reach the picture");
            Assert.That(item.Tiled.Source, Is.Not.Null, "putting the picture aside threw the file away");
        });

        fill.Owner.Write(fill, Stretch.UniformToFill);
        repeat.Owner.Write(repeat, TileMode.FlipXY);

        var shown = item.Tiled;

        Assert.Multiple(() =>
        {
            Assert.That(shown.Stretch, Is.EqualTo(Stretch.UniformToFill), "the fit never reached the picture");
            Assert.That(shown.TileMode, Is.EqualTo(TileMode.FlipXY), "the repeat never reached the picture");
        });

        // ...and the TILE's own rectangle, which is what makes repeating mean anything: a whole-surface tile repeats
        // once. Four lines, because a Rect cannot be written a side at a time.
        item.TileWidth = 0.25;
        item.TileHeight = 0.5;

        Assert.Multiple(() =>
        {
            Assert.That(shown.Viewport.Width, Is.EqualTo(0.25).Within(1e-9), "the tile never got its width");
            Assert.That(shown.Viewport.Height, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(item.TileWidth, Is.EqualTo(0.25).Within(1e-9), "the line does not read back what it wrote");
        });
    }

    // ITS REAL PROPORTIONS. The one thing about a picture nobody can type: a file is 1024 by 768 and there is no way
    // to know that until it is on the plane, so the panel reads it and one press takes it.
    [Test]
    public void ThePictureCanGiveTheTextureItsOwnSize()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var (window, canvas) = Stage();
        var item = Placed(canvas, new Rect(20, 20, 200, 150));

        Settle(window);

        var source = Row(canvas, "Source");

        source.Owner.Write(source, "C:/pictures/bark.png");

        Settle(window);

        var inspector = Found<CanvasInspector>(canvas);

        // The picture behind a path that names no real file has no size, and nothing is taken from it: a texture
        // squashed to nothing by a file that failed to load is worse than one left as it was.
        var was = new Vector2(item.Width, item.Height);

        inspector.ActualSizeCommand.Execute(null);

        var picture = item.Tiled?.Source;

        if (picture == null || picture.Width <= 0 || picture.Height <= 0)
        {
            Assert.That(new Vector2(item.Width, item.Height), Is.EqualTo(was),
                "a picture with no size of its own resized the texture anyway");
            return;
        }

        Assert.Multiple(() =>
        {
            Assert.That(item.Width, Is.EqualTo(picture.Width).Within(1e-9), "the width is not the picture's");
            Assert.That(item.Height, Is.EqualTo(picture.Height).Within(1e-9), "the height is not the picture's");
        });
    }

    // A composite is folded until somebody opens it, and a folded line has no row.
    private static void Open(IUIComponent within, Window window)
    {
        foreach (var row in Rows(within).ToList())
        {
            if (row.Definition is CompositeProperty composite) composite.IsExpanded = true;
        }

        foreach (var row in Rows(within).ToList()) row.Owner?.Rebuild();

        Settle(window);
    }

    private static T Found<T>(IUIComponent within) where T : class
    {
        if (within is T wanted) return wanted;

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual && Found<T>(visual) is { } deep) return deep;
        }

        return null;
    }

    // AND AN EDIT CAN BE TAKEN BACK. The panel has carried a reset button from the start and showed it nowhere,
    // because a line only offers one where "untouched" is known - either said by the markup, as a turn's nought is, or
    // known by the object itself.
    [Test]
    public void ATurnedControlOffersToPutItsTurnBack()
    {
        Use(new Adamantium.UI.Themes.FluentTheme.Fluent());

        var (window, canvas) = Stage();
        var item = Placed(canvas, new Rect(20, 20, 200, 150));

        Settle(window);

        // The turn lives in a composite, and a folded composite has no rows for its lines - opened exactly as a press
        // on its chevron opens it.
        if (Row(canvas, "Transform") is { } block) block.Owner.ToggleComposite(block);

        Settle(window);

        var turn = Row(canvas, "Angle");

        Assert.That(turn, Is.Not.Null, "the panel offers a selected control no turn");
        Assert.That(turn.IsModified, Is.False, "a control nobody turned offered to straighten it");

        turn.Owner.Write(turn, 30.0);

        Assert.That(turn.IsModified, Is.True, "a turned control offers no way back");

        turn.ResetToDefault();

        Assert.Multiple(() =>
        {
            Assert.That(item.Angle, Is.EqualTo(0).Within(1e-9), "the turn was not put back");
            Assert.That(turn.IsModified, Is.False, "the line still says it was turned");
        });
    }

    // ANYWHERE IN THE PANEL: the inspector holds more than one grid - the tool's own face and the selection's - and
    // which of them a line ended up in is not what this is asking.
    private static PropertyRow Row(IUIComponent within, string header) =>
        Rows(within).FirstOrDefault(row => row.Definition?.Header == header);

    private static System.Collections.Generic.IEnumerable<PropertyRow> Rows(IUIComponent within)
    {
        if (within is PropertyRow row) yield return row;

        foreach (var child in within.VisualChildren)
        {
            if (child is not IUIComponent visual) continue;

            foreach (var deep in Rows(visual)) yield return deep;
        }
    }
}
