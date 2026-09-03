using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Themes.MacOsTheme;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Where the macOS switch's thumb actually ends up, measured on the SHIPPED style set rather than a hand-built
/// lookalike - which is the whole point, because every wrong answer so far came from a reproduction that left out the
/// one thing that mattered.
/// <para>The thumb is held off the track by a single <c>Margin="2"</c> and only its ALIGNMENT moves, so off and on are
/// the same number at opposite ends. Anything else means something outside this template writes to the part.</para>
/// </summary>
[TestFixture]
public class MacOsSwitchThumbTests
{
    private const double TrackWidth = 38;
    private const double TrackHeight = 22;

    [Test]
    public void TheThumbSitsTwoFromWhicheverEndItIsAt()
    {
        var control = new ToggleSwitch { Content = "Diagnostics" };
        // A generated style set builds its styles in Initialize, not in its constructor. No theme is needed to place a
        // part - the {ResourceReference} setters are brushes, and an unresolved brush moves nothing.
        var set = new MacOsToggleSwitchStyleSet();
        set.Initialize(null);
        TestContext.WriteLine($"styles in the set: {set.Styles.Count}");
        foreach (var style in set.Styles)
        {
            style.Attach(control);
        }

        var root = new Border { Width = 300, Height = 80, Child = control };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);

        TestContext.WriteLine($"template: {control.Template?.GetType().Name ?? "<null>"}");
        var thumb = (IUIComponent)control.GetTemplateChild("SwitchThumb");
        Assert.That(thumb, Is.Not.Null, "the template names its thumb SwitchThumb");

        var off = thumb.Bounds;
        control.IsChecked = true;
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);
        var on = thumb.Bounds;

        TestContext.WriteLine($"thumb is a {thumb.GetType().Name}");
        TestContext.WriteLine($"off: {off}");
        TestContext.WriteLine($"on:  {on}");

        Assert.Multiple(() =>
        {
            Assert.That(off.Width, Is.EqualTo(18).Within(0.01), "off: the thumb keeps its size");
            Assert.That(off.Height, Is.EqualTo(18).Within(0.01));
            Assert.That(on.Width, Is.EqualTo(18).Within(0.01), "on: and keeps it when it moves");
            Assert.That(on.Height, Is.EqualTo(18).Within(0.01));

            Assert.That(off.X, Is.EqualTo(2).Within(0.01), "off: two from the left");
            Assert.That(TrackWidth - on.Right, Is.EqualTo(2).Within(0.01), "on: two from the right");
            Assert.That(off.Y, Is.EqualTo(2).Within(0.01), "and two from the top in both");
            Assert.That(on.Y, Is.EqualTo(2).Within(0.01));
            Assert.That(TrackHeight - off.Bottom, Is.EqualTo(2).Within(0.01));
        });
    }
}
