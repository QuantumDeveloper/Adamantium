using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

// Pure-CPU behaviour tests for the toggle family (ToggleButton/CheckBox/RadioButton/ToggleSwitch) and the range
// controls (Slider/ProgressBar) - their state machine and value math, independent of any template/GPU.
[TestFixture]
public class ControlsTests
{
    // ---- ToggleButton: click cycles the checked state, IsThreeState adds the indeterminate step ----

    [Test]
    public void ToggleButton_Click_TwoState_TogglesOnOff()
    {
        var toggle = new ToggleButton();
        Assert.That(toggle.IsChecked, Is.False);

        toggle.PerformClick();
        Assert.That(toggle.IsChecked, Is.True);

        toggle.PerformClick();
        Assert.That(toggle.IsChecked, Is.False, "two-state never goes indeterminate");
    }

    [Test]
    public void ToggleButton_Click_ThreeState_CyclesThroughIndeterminate()
    {
        var toggle = new ToggleButton { IsThreeState = true };

        toggle.PerformClick();
        Assert.That(toggle.IsChecked, Is.True);
        toggle.PerformClick();
        Assert.That(toggle.IsChecked, Is.Null, "third state is indeterminate");
        toggle.PerformClick();
        Assert.That(toggle.IsChecked, Is.False);
    }

    [Test]
    public void ToggleButton_RaisesCheckedAndUncheckedEvents()
    {
        var toggle = new ToggleButton();
        var checkedCount = 0;
        var uncheckedCount = 0;
        toggle.Checked += (_, _) => checkedCount++;
        toggle.Unchecked += (_, _) => uncheckedCount++;

        toggle.IsChecked = true;
        toggle.IsChecked = false;

        Assert.Multiple(() =>
        {
            Assert.That(checkedCount, Is.EqualTo(1));
            Assert.That(uncheckedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void CheckBox_IsAToggleButton()
    {
        var box = new CheckBox();
        box.PerformClick();
        Assert.That(box.IsChecked, Is.True);
    }

    // ---- RadioButton: one-of-many exclusivity by group ----

    [Test]
    public void RadioButton_SameGroup_CheckingOneClearsTheRest()
    {
        var group = "rb-" + Guid.NewGuid();
        var a = new RadioButton { GroupName = group };
        var b = new RadioButton { GroupName = group };
        var c = new RadioButton { GroupName = group };

        a.IsChecked = true;
        b.IsChecked = true;   // checking b must clear a

        Assert.Multiple(() =>
        {
            Assert.That(a.IsChecked, Is.False);
            Assert.That(b.IsChecked, Is.True);
            Assert.That(c.IsChecked, Is.False);
        });
    }

    [Test]
    public void RadioButton_Click_StaysCheckedNeverTogglesOff()
    {
        var radio = new RadioButton { GroupName = "rb-" + Guid.NewGuid() };
        radio.PerformClick();
        Assert.That(radio.IsChecked, Is.True);

        radio.PerformClick();   // a second click must NOT uncheck it
        Assert.That(radio.IsChecked, Is.True);
    }

    [Test]
    public void RadioButton_DifferentGroups_AreIndependent()
    {
        var a = new RadioButton { GroupName = "g1-" + Guid.NewGuid() };
        var b = new RadioButton { GroupName = "g2-" + Guid.NewGuid() };

        a.IsChecked = true;
        b.IsChecked = true;

        Assert.Multiple(() =>
        {
            Assert.That(a.IsChecked, Is.True, "different group is untouched");
            Assert.That(b.IsChecked, Is.True);
        });
    }

    // ---- ToggleSwitch: strictly on/off even if IsThreeState is set ----

    [Test]
    public void ToggleSwitch_IgnoresThreeState_OnlyFlipsOnOff()
    {
        var sw = new ToggleSwitch { IsThreeState = true };

        sw.PerformClick();
        Assert.That(sw.IsChecked, Is.True);
        sw.PerformClick();
        Assert.That(sw.IsChecked, Is.False, "a switch never lands on indeterminate");
        sw.PerformClick();
        Assert.That(sw.IsChecked, Is.True);
    }

    // ---- ProgressBar: filled fraction ----

    [Test]
    public void ProgressBar_Percentage_IsValueOverRange()
    {
        var bar = new ProgressBar { Minimum = 0, Maximum = 200, Value = 50 };
        Assert.That(bar.Percentage, Is.EqualTo(0.25).Within(1e-9));
    }

    [Test]
    public void ProgressBar_Percentage_AtBounds()
    {
        var bar = new ProgressBar { Minimum = 10, Maximum = 20 };
        bar.Value = 10;
        Assert.That(bar.Percentage, Is.EqualTo(0).Within(1e-9));
        bar.Value = 20;
        Assert.That(bar.Percentage, Is.EqualTo(1).Within(1e-9));
        bar.Value = 15;
        Assert.That(bar.Percentage, Is.EqualTo(0.5).Within(1e-9));
    }

    [Test]
    public void ProgressBar_DefaultsToHundredMaximum()
    {
        Assert.That(new ProgressBar().Maximum, Is.EqualTo(100));
    }

    // ---- Slider: range defaults + coercion (drag/positioning is the Track's, covered by ScrollControlsTests) ----

    [Test]
    public void Slider_Defaults_HorizontalZeroToHundred()
    {
        var slider = new Slider();
        Assert.Multiple(() =>
        {
            Assert.That(slider.Orientation, Is.EqualTo(Orientation.Horizontal));
            Assert.That(slider.Minimum, Is.EqualTo(0));
            Assert.That(slider.Maximum, Is.EqualTo(100));
        });
    }

    [Test]
    public void Slider_Value_ClampsToRange()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 250 };
        Assert.That(slider.Value, Is.EqualTo(100));
    }

    // ---- Classes -> ClassNames sync: what makes a class selector (e.g. "ProgressBar.Ring" -> the circular template)
    // actually match. Setting the Classes property, and later mutating that collection, must both reach ClassNames -
    // the collection Selector.Match reads.

    [Test]
    public void Classes_SetProperty_PopulatesClassNames()
    {
        var bar = new ProgressBar { Classes = Classes.Parse("Ring") };
        Assert.That(bar.ClassNames, Does.Contain("Ring"));
    }

    [Test]
    public void Classes_MutateCollection_UpdatesClassNames()
    {
        var bar = new ProgressBar { Classes = Classes.Parse("Ring") };
        bar.Classes.Add("Accent");
        Assert.Multiple(() =>
        {
            Assert.That(bar.ClassNames, Does.Contain("Ring"));
            Assert.That(bar.ClassNames, Does.Contain("Accent"));
        });
    }

    // ---- A non-Stretch ContentPresenter (e.g. a Button whose ContentPresenter binds HorizontalAlignment to the
    // control's HorizontalContentAlignment=Center) must shrink to its content and stay put - not collapse to zero and
    // spill the content outside. Repro for the button-chrome regression.

    [Test]
    public void ContentPresenter_CenterAligned_ShrinksToContent_NotZero()
    {
        var content = new Grid { Width = 100, Height = 30 };
        var cp = new ContentPresenter
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        cp.Measure(new Size(300, 100), force: true);
        cp.Arrange(new Rect(0, 0, 300, 100));

        Assert.Multiple(() =>
        {
            Assert.That(cp.DesiredSize.Width, Is.EqualTo(100).Within(1), "presenter desires its content's width");
            Assert.That(cp.ActualWidth, Is.EqualTo(100).Within(1), "centered presenter renders at content width, not 0");
            Assert.That(content.Bounds.Right, Is.LessThanOrEqualTo(cp.ActualWidth + 1), "content stays inside the presenter");
        });
    }

    [Test]
    public void ContentPresenter_CenterAligned_TextContent_DoesNotCollapse()
    {
        var cp = new ContentPresenter
        {
            Content = "Hello World",
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        cp.Measure(new Size(300, 100), force: true);
        cp.Arrange(new Rect(0, 0, 300, 100));

        Assert.That(cp.ActualWidth, Is.GreaterThan(0),
            $"text content collapsed: DesiredSize={cp.DesiredSize}, ActualWidth={cp.ActualWidth}");
    }

    // The real button chrome: a STRETCHED Border (decorator) wrapping a Center-aligned ContentPresenter (the WPF idiom
    // HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"). The border must fill its slot and centre the
    // content inside - it must NOT collapse to the content's width and let the text spill outside. Regression repro for
    // the button-chrome break (Decorator.ArrangeOverride was returning the child's size instead of the arranged size).
    [Test]
    public void StretchedDecorator_CenteredChild_FillsSlot_NotCollapse()
    {
        ContentPresenter cp = null;
        Border border = null;
        var template = new ControlTemplate(() =>
        {
            var result = new TemplateResult();
            cp = new ContentPresenter();
            border = new Border { Padding = new Thickness(11, 5, 11, 6), Child = cp };
            result.RegisterName("PART_ContentPresenter", cp);
            result.AddTemplateBinding(cp, "HorizontalAlignment", new TemplateBinding { Path = "HorizontalContentAlignment" });
            result.AddTemplateBinding(cp, "VerticalAlignment", new TemplateBinding { Path = "VerticalContentAlignment" });
            result.AddTemplateBinding(cp, "Content", new TemplateBinding { Path = "Content" });
            result.RootComponent = border;
            return result;
        });

        var button = new Button
        {
            Content = "Hello World",
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Template = template
        };

        button.Measure(new Size(300, 100), force: true);
        button.Arrange(new Rect(0, 0, 300, 100));

        Assert.Multiple(() =>
        {
            Assert.That(cp.HorizontalAlignment, Is.EqualTo(HorizontalAlignment.Center), "cross-name TemplateBinding mapped HorizontalContentAlignment onto the presenter");
            Assert.That(border.ActualWidth, Is.EqualTo(300).Within(1), $"stretched border fills its slot (border={border.Bounds}, cp={cp.Bounds})");
            Assert.That(cp.ActualWidth, Is.GreaterThan(0), "content did not collapse");
        });
    }

    // ---- RingProgressBar is INTRINSICALLY square (its own MeasureOverride, no attached marker): it derives the missing
    // dimension from the one given, so the ring stays round even when only Width or only Height is set. A linear ProgressBar
    // is never squared.

    [Test]
    public void RingProgressBar_OnlyWidthSet_MeasuresSquare()
    {
        var ring = new RingProgressBar { Width = 200 };   // Height left unset
        ring.Measure(new Size(500, 400), force: true);
        Assert.That(ring.DesiredSize.Height, Is.EqualTo(200).Within(1), "the height follows the set width");
    }

    [Test]
    public void RingProgressBar_OnlyHeightSet_MeasuresSquare()
    {
        var ring = new RingProgressBar { Height = 150 };   // Width left unset
        ring.Measure(new Size(500, 400), force: true);
        Assert.That(ring.DesiredSize.Width, Is.EqualTo(150).Within(1), "the width follows the set height");
    }

    [Test]
    public void ProgressBar_Linear_OnlyWidthSet_IsNotSquared()
    {
        var bar = new ProgressBar { Width = 200 };   // a linear bar is never squared
        bar.Measure(new Size(500, 400), force: true);
        Assert.That(bar.DesiredSize.Height, Is.LessThan(200), "a linear ProgressBar must not be forced square");
    }

    [Test]
    public void RingProgressBar_Centered_ArrangesSquare_NotStretched()
    {
        var ring = new RingProgressBar
        {
            Width = 200,   // Height unset
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        ring.Measure(new Size(500, 400), force: true);
        ring.Arrange(new Rect(0, 0, 500, 400));   // a slot far bigger than the ring - the open (height) axis must NOT stretch

        Assert.Multiple(() =>
        {
            Assert.That(ring.ActualWidth, Is.EqualTo(200).Within(1));
            Assert.That(ring.ActualHeight, Is.EqualTo(200).Within(1), "the unset axis must settle on the square, not stretch to the slot");
        });
    }

    [Test]
    public void RingProgressBar_MeasuresTemplateParts_AgainstTheSquare_NotTheHugeAvailable()
    {
        Ellipse part = null;
        var template = new ControlTemplate(() =>
        {
            var result = new TemplateResult();
            var grid = new Grid();
            part = new Ellipse();   // a stretch shape sizes its render rect to the space it is measured with
            grid.Children.Add(part);
            result.RegisterName("PART_Indicator", part);
            result.RootComponent = grid;
            return result;
        });

        var ring = new RingProgressBar { Template = template, Width = 200 };   // Height unset

        ring.Measure(new Size(2000, 2000), force: true);   // a HUGE available - the part must follow the 200 square, not this
        ring.Arrange(new Rect(0, 0, 2000, 2000));

        Assert.That(part.ActualWidth, Is.LessThan(300),
            $"the ring part must size to the 200 square, not the huge available (was {part.ActualWidth})");
    }
}
