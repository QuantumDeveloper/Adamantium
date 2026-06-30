using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// Pure-CPU regression tests for the Slider's accent fill (PART_SelectionRange). It must be ARRANGED to the value fraction
// the moment the control settles (not only after the first drag). Driven through the real LayoutManager (UpdateTree), not
// raw Measure/Arrange - a part resized after the first measure converges only through the manager's dirty queue.
[TestFixture]
public class SliderTests
{
    private static ControlTemplate SliderTemplate(Orientation orientation) => new(() =>
    {
        var vertical = orientation == Orientation.Vertical;
        var grid = new Grid();
        var rail = new Border { Height = 4 };
        var fill = new Border
        {
            Height = vertical ? 0 : 4,
            Width = vertical ? 4 : 0,
            HorizontalAlignment = vertical ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = vertical ? VerticalAlignment.Bottom : VerticalAlignment.Center
        };
        var track = new Track
        {
            Orientation = orientation,
            ViewportSize = 0,
            IsDirectionReversed = vertical,
            Thumb = new Thumb { Width = 20, Height = 20 }
        };
        grid.Children.Add(rail);
        grid.Children.Add(fill);
        grid.Children.Add(track);

        var result = new TemplateResult();
        result.RegisterName("PART_SelectionRange", fill);
        result.RegisterName("PART_Track", track);
        result.RootComponent = grid;
        return result;
    });

    private static void Settle(Border root)
    {
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(root);
    }

    // Isolation: a left-aligned Border whose Width is set AFTER the first layout must reach RenderSize through a normal
    // re-layout (the manager re-measures it). If this fails the bug is in Border/layout, not the Slider.
    [Test]
    public void Border_WidthChangedAfterLayout_ReachesRenderSize()
    {
        var border = new Border { Width = 0, Height = 4, HorizontalAlignment = HorizontalAlignment.Left };
        var grid = new Grid();
        grid.Children.Add(border);
        var root = new Border { Width = 240, Height = 24, Child = grid };
        Settle(root);

        border.Width = 96;
        Settle(root);

        Assert.That(border.RenderSize.Width, Is.EqualTo(96.0).Within(1.0));
    }

    [Test]
    public void Horizontal_InitialFill_SizedToValueFractionAfterLayout()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 40, Orientation = Orientation.Horizontal };
        slider.Template = SliderTemplate(Orientation.Horizontal);
        var root = new Border { Width = 240, Height = 24, Child = slider };
        Settle(root);

        var fill = (Border)slider.GetTemplateChild("PART_SelectionRange");
        var track = (Track)slider.GetTemplateChild("PART_Track");
        Assert.That(track.ActualWidth, Is.GreaterThan(0), "sanity: the track got a width");
        Assert.That(fill.RenderSize.Width, Is.EqualTo(0.40 * track.ActualWidth).Within(2.0),
            "accent fill must be ARRANGED to 40% of the track once layout settles, not 0 until the first drag");
    }

    [Test]
    public void Vertical_InitialFill_SizedToValueFractionAfterLayout()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 40, Orientation = Orientation.Vertical };
        slider.Template = SliderTemplate(Orientation.Vertical);
        var root = new Border { Width = 24, Height = 180, Child = slider };
        Settle(root);

        var fill = (Border)slider.GetTemplateChild("PART_SelectionRange");
        var track = (Track)slider.GetTemplateChild("PART_Track");
        Assert.That(track.ActualHeight, Is.GreaterThan(0), "sanity: the track got a height");
        Assert.That(fill.RenderSize.Height, Is.EqualTo(0.40 * track.ActualHeight).Within(2.0),
            "accent fill must be ARRANGED to 40% of the track height once layout settles");
    }

    [Test]
    public void Horizontal_ValueChange_RegrowsFill()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 20, Orientation = Orientation.Horizontal };
        slider.Template = SliderTemplate(Orientation.Horizontal);
        var root = new Border { Width = 240, Height = 24, Child = slider };
        Settle(root);

        var fill = (Border)slider.GetTemplateChild("PART_SelectionRange");
        var at20 = fill.RenderSize.Width;

        slider.Value = 80;
        Settle(root);

        Assert.That(fill.RenderSize.Width, Is.GreaterThan(at20), "raising the value must grow the accent fill");
    }
}
