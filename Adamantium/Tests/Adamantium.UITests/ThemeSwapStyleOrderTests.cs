using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A theme swap replaces one set of styles with another on the same controls, and the ORDER of that replacement is not
/// a free choice. A setter undoes its own contribution only when the undo is keyed by style - which is true of a plain
/// value (RemoveStyleValue takes the style) and NOT of the marker kinds: a {Binding}, {ThemeResource}, {Ancestor} or
/// {Self} setter is undone by (component, property) alone. So the outgoing set has to be taken off BEFORE the incoming
/// one is applied; do it the other way round and the old theme's teardown removes the new theme's value.
/// </summary>
[TestFixture]
public class ThemeSwapStyleOrderTests
{
    private sealed class Source
    {
        public double Value { get; init; }
    }

    private static Style WidthFrom(Source source)
    {
        var style = new Style();
        style.Selector.Types.Add(typeof(Border));
        style.Setters.Add(new Setter(nameof(Border.Width), new Binding(nameof(Source.Value)) { Source = source }));
        return style;
    }

    private static Style WidthOf(double width)
    {
        var style = new Style();
        style.Selector.Types.Add(typeof(Border));
        style.Setters.Add(new Setter(nameof(Border.Width), width));
        return style;
    }

    /// <summary>
    /// The ordinary setter - a plain value, which is what a theme is almost entirely made of (Template, Background, every
    /// brush). Two styles contribute to one property and the FIRST is taken away: what is left must be the second's
    /// value, because that is the one still applied.
    /// <para>This is the white-empty-window bug. The style values are a stack that assumed removals happen in reverse
    /// order of application, so taking out anything but the top handed back "the entry before it" - nothing, when the
    /// outgoing theme's entry sat at the bottom - and that nothing was written into the property. A window whose
    /// Template and Background were wiped renders as blank white.</para>
    /// </summary>
    [Test]
    public void RemovingTheFIRSTOfTwoContributions_LeavesTheOtherOnesValue()
    {
        var border = new Border();
        var outgoing = WidthOf(40);
        var incoming = WidthOf(90);

        outgoing.Attach(border);
        incoming.Attach(border);
        Assert.That(border.Width, Is.EqualTo(90), "the last style applied is the one in force");

        outgoing.Detach(border);

        Assert.That(border.Width, Is.EqualTo(90),
            "taking away a style that was NOT the one in force must not disturb the one that is");
    }

    /// <summary>And the other direction, which always worked: removing the style that IS in force falls back to the one
    /// underneath it, rather than to nothing.</summary>
    [Test]
    public void RemovingTheONEInForce_FallsBackToTheOtherContribution()
    {
        var border = new Border();
        var under = WidthOf(40);
        var over = WidthOf(90);

        under.Attach(border);
        over.Attach(border);
        over.Detach(border);

        Assert.That(border.Width, Is.EqualTo(40));
    }

    /// <summary>The swap as it happens: the outgoing theme's styles are detached, the incoming theme's applied. What the
    /// control shows afterwards is the incoming theme's - whichever order the two halves ran in.</summary>
    [Test]
    public void DetachingTheOutgoingStyle_LeavesTheIncomingOnesValue()
    {
        var border = new Border();
        var outgoing = WidthFrom(new Source { Value = 40 });
        var incoming = WidthFrom(new Source { Value = 90 });

        outgoing.Attach(border);
        Assert.That(border.Width, Is.EqualTo(40), "the outgoing theme is what is on screen before the swap");

        incoming.Attach(border);
        outgoing.Detach(border);   // the old set goes away AFTER the new one landed

        Assert.That(border.Width, Is.EqualTo(90),
            "the incoming theme's value survives the outgoing one's teardown - a marker setter is undone by property, " +
            "not by style, so the old style's Remove would otherwise take the new style's value with it");
    }
}
