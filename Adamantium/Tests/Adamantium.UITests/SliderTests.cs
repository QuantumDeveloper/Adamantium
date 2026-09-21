using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
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

    // Mirrors the real theme: the Track's Minimum/Maximum/Value come from the Slider via {TemplateBinding}. This is the
    // path the hand-built SliderTemplate above skips (it sets Track props directly), so it's the one that can silently
    // fail and pin the thumb at the start.
    private static ControlTemplate SliderTemplateWithTemplateBindings() => new(() =>
    {
        var grid = new Grid();
        var track = new Track { Orientation = Orientation.Horizontal, ViewportSize = 0, Thumb = new Thumb { Width = 20, Height = 20 } };
        grid.Children.Add(track);

        var result = new TemplateResult { RootComponent = grid };
        result.RegisterName("PART_Track", track);
        result.AddTemplateBinding(track, "Minimum", new TemplateBinding { Path = "Minimum" });
        result.AddTemplateBinding(track, "Maximum", new TemplateBinding { Path = "Maximum" });
        result.AddTemplateBinding(track, "Value", new TemplateBinding { Path = "Value" });
        return result;
    });

    private static void Settle(Border root)
    {
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(root);
    }

    // The live-app regression: the theme feeds Track.Value/Minimum/Maximum via {TemplateBinding}. If that binding doesn't
    // carry the Slider's initial non-default Value into the Track, the Track positions the thumb for Value=Minimum -> at
    // the very start, even though the Slider's Value (and the accent fill) are correct.
    [Test]
    public void Slider_ThumbAtInitialValue_ViaTemplateBinding()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 75, Orientation = Orientation.Horizontal };
        slider.Template = SliderTemplateWithTemplateBindings();
        var root = new Border { Width = 220, Height = 24, Child = slider };
        Settle(root);

        var track = (Track)slider.GetTemplateChild("PART_Track");
        Assert.Multiple(() =>
        {
            Assert.That(track.Value, Is.EqualTo(75).Within(0.5), "Track.Value must receive the Slider's Value via {TemplateBinding Value}");
            Assert.That(track.Thumb.Bounds.X, Is.GreaterThan(100), "thumb sits at the value fraction, not pinned at the start");
        });
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
        var thumb = track.Thumb.Bounds;
        Assert.Multiple(() =>
        {
            Assert.That(track.ActualWidth, Is.GreaterThan(0), "sanity: the track got a width");
            Assert.That(fill.RenderSize.Width, Is.GreaterThan(0), "the fill must be arranged once layout settles, not 0 until the first drag");
            // Against the THUMB, not against a second projection of the value: the thumb travels the trough minus its own
            // width, so measuring the fill as a fraction of the FULL width leaves the two half a thumb apart at the ends.
            Assert.That(fill.RenderSize.Width, Is.EqualTo(thumb.X + thumb.Width / 2).Within(1.0),
                "the accent fill must end at the thumb");
        });
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
        var thumb = track.Thumb.Bounds;
        Assert.Multiple(() =>
        {
            Assert.That(track.ActualHeight, Is.GreaterThan(0), "sanity: the track got a height");
            Assert.That(fill.RenderSize.Height, Is.GreaterThan(0), "the fill must be arranged once layout settles");
            // Upright and reversed: the fill grows from the BOTTOM up to the thumb, so it ends where the thumb's centre is.
            Assert.That(fill.RenderSize.Height, Is.EqualTo(track.ActualHeight - (thumb.Y + thumb.Height / 2)).Within(1.0),
                "the accent fill must end at the thumb");
        });
    }

    // The THUMB (not just the accent fill) must sit at the Value fraction. The theme binds Track.Value={TemplateBinding
    // Value}; the Track positions the thumb in ArrangeOverride. Regression: a slider whose initial Value != Minimum showed
    // the thumb pinned at the start. These drive the Track directly (Value set on it) to isolate the Track's arrange from
    // the TemplateBinding that feeds it.
    private sealed class ValueSource { public double V { get; init; } }

    // The real slider bug: Value is data-bound AND coerced (RangeBase clamps to [Min,Max]). Minimum is applied first
    // (during construction, coercing the default 0 up to Min) and the {Binding} resolves later (once DataContext is
    // set). If the Minimum-coercion writes Value at LOCAL priority, it outranks the {Binding} (Local=1 beats Binding=2)
    // and the slider stays pinned at Minimum forever - the thumb sat at the start. The coerced default must sit at its
    // own (default) priority so the binding can still apply.
    [Test]
    public void BoundValue_WithMinimumMaximum_AppliesBindingOverCoercedMinimum()
    {
        var source = new ValueSource { V = 64 };
        var slider = new Slider();
        slider.Minimum = 24;    // coerces the default Value (0) up to 24 - must NOT be promoted above a binding
        slider.Maximum = 180;
        slider.SetBinding("Value", new Binding("V"));   // DataContext still null here: doesn't resolve yet
        slider.DataContext = source;                    // now the binding re-resolves and must push 64

        Assert.That(slider.Value, Is.EqualTo(64).Within(0.5),
            "a {Binding} on Value must win over the Minimum-coercion; the coerced default must not outrank the binding");
    }

    [Test]
    public void Track_ThumbSitsAtInitialValueFraction()
    {
        var thumb = new Thumb { Width = 20, Height = 20 };
        var track = new Track { Orientation = Orientation.Horizontal, Minimum = 0, Maximum = 100, Value = 75, ViewportSize = 0, Thumb = thumb };
        var root = new Border { Width = 220, Height = 24, Child = track };
        Settle(root);

        // trackLength 220, thumb 20 -> remaining 200; offset = 0.75 * 200 = 150.
        Assert.That(thumb.Bounds.X, Is.EqualTo(150).Within(3),
            "thumb must start at the initial Value fraction, not pinned at the track start");
    }

    [Test]
    public void Track_ThumbRepositionsOnValueChange()
    {
        var thumb = new Thumb { Width = 20, Height = 20 };
        var track = new Track { Orientation = Orientation.Horizontal, Minimum = 0, Maximum = 100, Value = 0, ViewportSize = 0, Thumb = thumb };
        var root = new Border { Width = 220, Height = 24, Child = track };
        Settle(root);
        var xAtMin = thumb.Bounds.X;

        track.Value = 100;
        Settle(root);

        Assert.Multiple(() =>
        {
            Assert.That(xAtMin, Is.EqualTo(0).Within(1), "at Value=Minimum the thumb sits at the start");
            Assert.That(thumb.Bounds.X, Is.GreaterThan(xAtMin + 150), "raising Value moves the thumb toward the end");
        });
    }

    // The exact live-app order: the Slider's Value arrives LATER, from a {Binding} that resolves once the DataContext
    // flows - i.e. AFTER the template's {TemplateBinding Value} was established at Value=0. The Track must FOLLOW that
    // change. If the template binding is not live, Track.Value stays 0 and the thumb never leaves the start (the bug).
    [Test]
    public void Slider_ThumbFollowsLaterValueChange_ViaTemplateBinding()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 0, Orientation = Orientation.Horizontal };
        slider.Template = SliderTemplateWithTemplateBindings();
        var root = new Border { Width = 220, Height = 24, Child = slider };
        Settle(root);
        var track = (Track)slider.GetTemplateChild("PART_Track");
        var xAt0 = track.Thumb.Bounds.X;

        slider.Value = 75;   // as a {Binding} would set it after the template already bound at 0
        Settle(root);

        Assert.Multiple(() =>
        {
            Assert.That(track.Value, Is.EqualTo(75).Within(0.5), "Track.Value must follow a later Slider.Value change via {TemplateBinding}");
            Assert.That(track.Thumb.Bounds.X, Is.GreaterThan(xAt0 + 100), "thumb follows the later value change");
        });
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
