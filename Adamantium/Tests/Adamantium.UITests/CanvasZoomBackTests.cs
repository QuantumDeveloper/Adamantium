using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>ZOOMED IN UNTIL NOTHING IS IN VIEW, AND BACK OUT AGAIN. What is on the plane never changed; what is drawn
/// has to come back with it.</summary>
public class CanvasZoomBackTests
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

    private static void Lay(InfiniteCanvas canvas)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            canvas.Measure(new Size(800, 600), force: true);
            canvas.Arrange(new Rect(0, 0, 800, 600));
            Adamantium.UI.Extensions.WindowExtension.UpdateTree(canvas);
        }
    }

    private static int Drawn(InfiniteCanvas canvas)
    {
        var count = 0;

        void Look(IUIComponent at)
        {
            if (at is CanvasDrawLayer drawn) count += drawn.Painted.Length;

            foreach (var child in at.VisualChildren) Look(child);
        }

        Look(canvas);
        return count;
    }

    [Test]
    public void WhatWasDrawnComesBackWhenTheCameraDoes()
    {
        var canvas = new InfiniteCanvas
        {
            Template = Template(),
            Scene = new CanvasScene(),
            Width = 800,
            Height = 600
        };

        canvas.Scene.Add(new ShapeItem(CanvasShape.Rectangle, new Rect(-200, -80, 160, 90),
            new SolidColorBrush(Colors.White), 2));
        canvas.Scene.Add(new ShapeItem(CanvasShape.Ellipse, new Rect(40, 20, 120, 70),
            new SolidColorBrush(Colors.White), 2));

        Lay(canvas);
        canvas.Offset = new Vector2(400, 300);
        Lay(canvas);

        Assert.That(Drawn(canvas), Is.EqualTo(2), "nothing was being drawn to begin with");

        // ALL THE WAY IN, which is where it was reported from: the viewport then holds a patch of plane a hundredth of
        // a unit across.
        canvas.Scale = canvas.MaxScale;
        canvas.CenterOn(new Vector2(600, 600));
        Lay(canvas);

        Assert.That(Drawn(canvas), Is.Zero, "the deep zoom was still showing something");

        canvas.Scale = 0.8;
        canvas.CenterOn(Vector2.Zero);
        Lay(canvas);

        Assert.That(Drawn(canvas), Is.EqualTo(2), "the drawing did not come back with the camera");
    }
}
