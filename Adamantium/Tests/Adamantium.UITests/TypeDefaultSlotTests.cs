using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The <see cref="ValuePriority.TypeDefault"/> slot, exercised through REAL styles rather than by writing a
/// slot by hand - the point being that the engine picks the slot, and picks it from the selector.
/// <para>What it buys: a blanket <c>Style Selector="TextBlock"</c> - the most natural style anyone would write, and the
/// one the themes could not have - no longer cuts the channel a control uses to recolour its own content. Before this
/// slot existed, writing that style anywhere in an application silently stopped every selected row, pressed button and
/// disabled label from following its state, with nothing in any log to say so.</para>
/// </summary>
[TestFixture]
public class TypeDefaultSlotTests
{
    private static Style TypeStyle<T>(string property, object value)
    {
        var style = new Style();
        style.Selector.Types.Add(typeof(T));
        style.Setters.Add(new Setter(property, value));
        return style;
    }

    private static Style ClassStyle<T>(string className, string property, object value)
    {
        var style = new Style();
        style.Selector.Types.Add(typeof(T));
        style.Selector.Classes.Add(className);
        style.Setters.Add(new Setter(property, value));
        return style;
    }

    /// <summary>A bare-type style still dresses loose text: with nothing above saying otherwise, the type default is
    /// what stands. This is the half that must NOT be lost - a weaker slot is only useful if it still applies.</summary>
    [Test]
    public void ABareTypeStyle_StillDressesLooseText()
    {
        var text = new TextBlock { Text = "loose" };
        TypeStyle<TextBlock>("Foreground", Brushes.Blue).Attach(text);

        TestContext.WriteLine($"loose TextBlock under a bare-type style={text.Foreground}");
        Assert.That(text.Foreground, Is.SameAs(Brushes.Blue));
    }

    /// <summary>...and it LOSES to what an ancestor actually says, which is the whole change. The presenter of a
    /// selected row holds the accent; the text inside it follows the row, not the blanket rule.
    /// <para>Read together with the test above, not alone: on its own this assertion would also pass if the style had
    /// never applied at all. The pair is what makes it mean something - the SAME style, built the same way, colours a
    /// loose TextBlock blue, so here it is losing rather than absent.</para></summary>
    [Test]
    public void ABareTypeStyle_LosesToWhatAnAncestorSays()
    {
        var text = new TextBlock { Text = "row" };
        var presenter = new Border { Width = 200, Height = 40, Foreground = Brushes.Red, Child = text };
        TypeStyle<TextBlock>("Foreground", Brushes.Blue).Attach(text);
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(presenter);

        TestContext.WriteLine($"ancestor says Red, blanket style says Blue, text={text.Foreground}");
        Assert.That(text.Foreground, Is.SameAs(Brushes.Red),
            "the row's colour reaches its own text - a blanket type rule no longer masks it");
    }

    /// <summary>A selector that NARROWS is not a default and keeps its full strength: someone who writes
    /// <c>TextBlock.Caption</c> is talking about particular elements and means it.</summary>
    [Test]
    public void AClassStyle_StillOutranksInheritance()
    {
        // Classes must be ASSIGNED, not added to a freshly read collection: the assignment is what syncs ClassNames,
        // which is the collection the selector actually reads. Added to the empty one, the class never existed and the
        // style silently did not match - which made an earlier version of this test pass its assertion for the wrong
        // reason entirely.
        var text = new TextBlock { Text = "caption", Classes = Classes.Parse("Caption") };
        var presenter = new Border { Width = 200, Height = 40, Foreground = Brushes.Red, Child = text };
        ClassStyle<TextBlock>("Caption", "Foreground", Brushes.Blue).Attach(text);
        TestContext.WriteLine($"straight after Attach, before the tree runs: {text.Foreground}");
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(presenter);

        TestContext.WriteLine($"ancestor says Red, class style says Blue, text={text.Foreground}");
        Assert.That(text.Foreground, Is.SameAs(Brushes.Blue),
            "a narrowing selector is a statement about these elements, not a default for the type");
    }

    /// <summary>A NON-inheritable property is untouched by any of this: there is no inheritance for it to lose to, so a
    /// bare-type style keeps writing at Style priority exactly as before. This is what keeps every control's own
    /// Background, CornerRadius and Padding working while the change is in.</summary>
    [Test]
    public void ANonInheritableProperty_IsUnaffected()
    {
        var border = new Border { Width = 40, Height = 40 };
        TypeStyle<Border>("Background", Brushes.Blue).Attach(border);

        TestContext.WriteLine($"Border.Background under a bare-type style={border.Background}");
        Assert.That(border.Background, Is.SameAs(Brushes.Blue));
    }

    /// <summary>Two ancestors, and the NEAREST one wins - unchanged by the new slot, and worth pinning because the slot
    /// sits next door to the inheritance machinery.</summary>
    [Test]
    public void TheNearestAncestorStillWins()
    {
        var text = new TextBlock { Text = "row" };
        var inner = new Border { Foreground = Brushes.Green, Child = text };
        var outer = new Border { Width = 200, Height = 40, Foreground = Brushes.Red, Child = inner };
        TypeStyle<TextBlock>("Foreground", Brushes.Blue).Attach(text);
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(outer);

        TestContext.WriteLine($"outer=Red, inner=Green, blanket=Blue, text={text.Foreground}");
        Assert.That(text.Foreground, Is.SameAs(Brushes.Green));
    }
}
