using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Themes.MacOsTheme;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// A segment sits in the middle of its groove. Off-centre by a pixel or two is the kind of thing that has no name on
/// screen - it just looks wrong - so it is measured here rather than looked at.
/// </summary>
[TestFixture]
public class MacOsTabCapsuleCentringTests
{
    private FakeApp _app;
    private ThemeManager _themes;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    [SetUp]
    public void Fresh()
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);
        _themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = _themes;
        ((FakeContext)_app.UIContext).ThemeEngine = _themes;

        var theme = new MacOs();
        _themes.AddTheme(theme.Name, theme);
        _themes.SetTheme(theme);
    }

    [Test]
    public void TheCapsuleSitsInTheMiddleOfTheGroove()
    {
        // Mirrors the gallery, which is where this was seen: close buttons ON, which is what makes a tab taller than
        // its label and is the difference between a strip that centres and one that does not.
        var control = new TabControl { ShowCloseButton = true };
        control.Items.Add(new TabItem { Header = "One" });
        control.Items.Add(new TabItem { Header = "Two" });
        control.SelectedIndex = 0;
        control.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(control);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        // The chevron is what a strip full of tabs puts beside them, and it is the tallest thing in that row - so it
        // is part of the geometry being asked about. Revealed by hand because the control shows it post-layout, only
        // once the strip actually overflows.
        var chevron = (UIComponent)control.GetTemplateChild("PART_TabOverflow");
        chevron.Visibility = Visibility.Visible;

        control.Measure(new Size(600, 400));
        control.Arrange(new Rect(0, 0, 600, 400));

        var track = (UIComponent)control.GetTemplateChild("StripTrack");
        var strip = (UIComponent)control.GetTemplateChild("PART_TabStrip");
        var indicator = (UIComponent)control.GetTemplateChild("PART_SelectionIndicator");
        var tab = (TabItem)control.Items[0];
        var capsule = (UIComponent)tab.GetTemplateChild("TabBorder");

        // WORLD position, not Bounds: Bounds is the element's own rect in its own space and carries none of its
        // ancestors' padding, which is exactly the offset in question here.
        static double Top(UIComponent c) => c.WorldTransform.TranslationVector.Y;

        TestContext.WriteLine($"track     top={Top(track)} h={track.RenderSize.Height}");
        TestContext.WriteLine($"strip     top={Top(strip)} h={strip.RenderSize.Height}");
        TestContext.WriteLine($"tab       top={Top(tab)} h={tab.RenderSize.Height}");
        TestContext.WriteLine($"capsule   top={Top(capsule)} h={capsule.RenderSize.Height}");
        TestContext.WriteLine($"indicator top={Top(indicator)} h={indicator.RenderSize.Height} " +
                              $"thickness={control.SelectionIndicatorThickness}");
        TestContext.WriteLine($"chevron   top={Top(chevron)} h={chevron.RenderSize.Height}");

        var trackTop = Top(track);
        var trackBottom = trackTop + track.RenderSize.Height;
        var capsuleTop = Top(capsule);
        var capsuleBottom = capsuleTop + capsule.RenderSize.Height;

        var above = capsuleTop - trackTop;
        var below = trackBottom - capsuleBottom;
        TestContext.WriteLine($"gap above={above}, gap below={below}");

        Assert.Multiple(() =>
        {
            Assert.That(System.Math.Abs(above - below), Is.LessThanOrEqualTo(0.5),
                "the groove's padding has to be the same on both sides of the segment");

            // The invariant behind it, stated so a future change cannot bring the symptom back by another route: the
            // chevron is chrome standing IN the groove, and anything in there that is taller than a segment takes the
            // row's height with it and pushes the segments off centre.
            Assert.That(chevron.RenderSize.Height, Is.LessThanOrEqualTo(capsule.RenderSize.Height),
                "nothing in the groove may be taller than the segments it holds");

            // ...and it still has to be worth aiming at.
            Assert.That(chevron.RenderSize.Height, Is.GreaterThanOrEqualTo(18), "the chevron is a target, not a hairline");
        });
    }

    // The chevron lost its hover on the way over from Fluent, and for a reason worth a test rather than a fix: this
    // theme's ToggleButton has NO hover trigger at all, deliberately - a bordered control here does not light up. The
    // chevron is not one of those; it is chrome you aim at, and chrome hovers.
    [Test]
    public void TheOverflowChevronAnswersThePointer()
    {
        var control = new TabControl();
        control.Items.Add(new TabItem { Header = "One" });
        control.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(control);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        var chevron = (Adamantium.UI.Controls.Primitives.ToggleButton)control.GetTemplateChild("PART_TabOverflow");
        chevron.Visibility = Visibility.Visible;
        control.Measure(new Size(600, 400));
        control.Arrange(new Rect(0, 0, 600, 400));

        var plate = (Adamantium.UI.Controls.Decorators.Border)chevron.GetTemplateChild("ChevronBg");
        Assert.That(plate, Is.Not.Null, "the chevron carries a plate of its own to light up");

        // Written through the property itself: the CLR setter is private, because only the input pass gets to say the
        // pointer is over something.
        var atRest = plate.Background;
        chevron.SetValue(InputUIComponent.IsMouseOverProperty, true);
        var hovered = plate.Background;
        chevron.SetValue(InputUIComponent.IsMouseOverProperty, false);
        var afterwards = plate.Background;

        TestContext.WriteLine($"rest={atRest}, hover={hovered}, after={afterwards}");
        Assert.Multiple(() =>
        {
            Assert.That(hovered, Is.Not.SameAs(atRest), "the pointer has to change something");
            Assert.That(afterwards, Is.SameAs(atRest), "...and leaving has to put it back");
        });
    }
}
