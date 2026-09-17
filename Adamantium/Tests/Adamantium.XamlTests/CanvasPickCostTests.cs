using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>WHAT A CLICK COSTS on a plane holding nodes, and where the time goes.
/// <para>A press feels slow once nodes are on the plane and did not before, and there are three candidates a guess
/// cannot tell apart: the scene is walked linearly to find what was hit; a node is a CONTROL with a subtree of its own,
/// so hit-testing walks that too; and a press touches the scene, which may set a layout pass going. This times each of
/// them on its own and writes the numbers to a file, because a number nobody can read afterwards settles nothing.</para>
/// </summary>
[TestFixture]
public class CanvasPickCostTests
{
    private FakeApp _app;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    private void Use()
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);

        var themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = themes;
        ((FakeContext)_app.UIContext).ThemeEngine = themes;

        var theme = new Adamantium.UI.Themes.FluentTheme.Fluent();
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);
    }

    // The stand's own scene, near enough: a handful of shapes and two nodes.
    private static (InfiniteCanvas Canvas, CanvasScene Scene, CanvasElementLayer Layer, Window Window) Plane(int nodes)
    {
        var canvas = new InfiniteCanvas();
        canvas.Measure(new Size(1200, 800), force: true);
        canvas.Arrange(new Rect(0, 0, 1200, 800));

        var scene = new CanvasScene();
        var elements = new List<ElementItem>();

        for (var i = 0; i < 8; i++)
        {
            scene.Add(new ShapeItem(CanvasShape.Rectangle, new Rect(i * 40, 300, 30, 30), Brushes.White, 2));
        }

        for (var i = 0; i < nodes; i++)
        {
            var item = new ElementItem(new CanvasNode { Title = "N" + i, Inputs = 3, Outputs = 2 },
                new Rect(i * 220, 0, 190, 130));

            scene.Add(item);
            elements.Add(item);
        }

        canvas.Scene = scene;
        canvas.Tool = new SelectTool();

        var layer = new CanvasElementLayer { Owner = canvas };
        layer.Sync(elements);

        var window = new Window { Width = 1200, Height = 800, Content = layer };
        for (var i = 0; i < 6; i++)
        {
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        }

        return (canvas, scene, layer, window);
    }

    private static double Millis(int times, Action what)
    {
        what();   // once outside the clock: the first of anything pays for what it had to build

        var clock = Stopwatch.StartNew();
        for (var i = 0; i < times; i++) what();
        clock.Stop();

        return clock.Elapsed.TotalMilliseconds / times;
    }

    // NOT a threshold on a number that depends on the machine - what this asserts is the SHAPE: a press on a plane with
    // ten nodes must not cost ten times what it costs with one. Anything that walks everything on the plane does.
    [Test]
    public void APressDoesNotCostMoreForEveryNodeOnThePlane()
    {
        Use();

        var said = new System.Text.StringBuilder();
        var costs = new double[2];
        var counts = new[] { 1, 10 };

        for (var i = 0; i < counts.Length; i++)
        {
            var (canvas, scene, _, window) = Plane(counts[i]);
            var at = new Vector2(2000, 2000);   // empty plane: nothing is hit, so what is timed is the SEARCH

            var scan = Millis(200, () =>
            {
                foreach (var item in scene.ItemsIn(new Rect(at.X - 4, at.Y - 4, 8, 8))) _ = item.Bounds;
            });

            var press = Millis(200, () => canvas.Tool.OnPressed(canvas, new CanvasPointerEventArgs
            {
                World = at,
                Pointer = at,
                Screen = canvas.WorldToScreen(at),
                Button = Adamantium.UI.Core.Input.MouseButtons.Left
            }));

            canvas.Tool.Cancel(canvas);

            // ...and what a press SETS GOING. Touching the scene is what every gesture ends with, and if that puts a
            // layout pass over every node through, the press is cheap and the frame after it is not - which is what a
            // click that feels slow actually is.
            var settle = Millis(20, () =>
            {
                scene.Touch();
                Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
            });

            costs[i] = press;
            said.AppendLine($"{counts[i]} nodes: scan {scan:F4} ms, press {press:F4} ms, settle {settle:F4} ms");
        }

        File.WriteAllText("canvas-pick-cost.log", said.ToString());
        TestContext.Out.Write(said.ToString());

        // Ten times the nodes for at most four times the press: some growth is honest (the scene IS walked once), a
        // multiple of the count is not.
        Assert.That(costs[1], Is.LessThan(Math.Max(costs[0], 1e-4) * 4),
            "a press costs in proportion to how much is on the plane:\r\n" + said);
    }
}
