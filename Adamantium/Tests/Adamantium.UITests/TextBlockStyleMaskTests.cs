using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Why the themes deliberately keep TextBlockStyleSet EMPTY, measured rather than argued.
/// <para>The objection this answers is a reasonable one: surely a per-TextBlock style would be fine as long as its
/// value came from the theme - <c>{ThemeResource}</c> rather than a hard-coded brush? It would not, and the reason has
/// nothing to do with where the value comes from. A Setter puts its value in the <see cref="ValuePriority.Style"/>
/// slot whatever the markup extension is (Setter.cs applies ThemeResource and ObservableResource at exactly that
/// priority), and Style outranks Inherited. Inheritance is the channel a control uses to recolour its OWN content on a
/// state change, so a TextBlock style masks it.</para>
/// </summary>
[TestFixture]
public class TextBlockStyleMaskTests
{
    /// <summary>The channel itself: an ancestor that SETS a foreground hands it to the text inside. This is what a
    /// selected row does - its trigger writes the accent onto its content presenter, and the text below follows.</summary>
    [Test]
    public void AnAncestorsForeground_ReachesTheTextInside()
    {
        var text = new TextBlock { Text = "row" };
        var presenter = new Border { Width = 200, Height = 40, Foreground = Brushes.Red, Child = text };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(presenter);

        TestContext.WriteLine($"ancestor Foreground=Red, TextBlock Foreground={text.Foreground}");
        Assert.That(text.Foreground, Is.SameAs(Brushes.Red), "state recolouring travels by INHERITANCE");
    }

    /// <summary>...and a per-TextBlock STYLE cuts that channel. The style value here is applied exactly as
    /// <see cref="Adamantium.UI.Core.Resources.Setter"/> applies one - at Style priority - which is what a
    /// <c>&lt;Style Selector="TextBlock"&gt;</c> would produce no matter which resource extension wrote it.</summary>
    [Test]
    public void ATextBlockStyle_MasksTheAncestorsForeground()
    {
        var text = new TextBlock { Text = "row" };
        var presenter = new Border { Width = 200, Height = 40, Foreground = Brushes.Red, Child = text };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(presenter);

        text.SetValue(TextBlock.ForegroundProperty, Brushes.Blue, ValuePriority.Style);

        TestContext.WriteLine($"ancestor Foreground=Red, TextBlock after a Style-priority setter={text.Foreground}");
        Assert.That(text.Foreground, Is.SameAs(Brushes.Blue),
            "Style (5) outranks Inherited (6), so the selected row's colour never reaches its own text");
    }

    /// <summary>How far the damage actually reaches - measured, because "the whole theme breaks" and "one case breaks"
    /// call for different fixes. Content given as a STRING is immune: the presenter GENERATES the TextBlock and stamps
    /// its own colour on it as a LOCAL value (ContentPresenter.ApplyTextStyle), and Local (1) beats Style (5). A row
    /// whose item is a plain string keeps following selection even with a blanket TextBlock style in the theme.</summary>
    [Test]
    public void GeneratedTextContent_IsImmuneToATextBlockStyle()
    {
        var presenter = new ContentPresenter { Content = "row", Foreground = Brushes.Red };
        presenter.Measure(new Size(120, 30));
        var generated = presenter.VisualChildren.OfType<TextBlock>().FirstOrDefault();
        Assert.That(generated, Is.Not.Null, "string content is hosted in an auto-generated TextBlock");

        generated.SetValue(TextBlock.ForegroundProperty, Brushes.Blue, ValuePriority.Style);

        TestContext.WriteLine($"generated TextBlock after a Style-priority setter={generated.Foreground}");
        Assert.That(generated.Foreground, Is.SameAs(Brushes.Red),
            "the presenter's LOCAL stamp outranks a blanket style - this half of the app cannot be broken by one");
    }

    /// <summary>...and AUTHORED content is the exposed half. A TextBlock written out in a DataTemplate is not stamped -
    /// deliberately, since an explicit write would become the element's own colour for good (see the docking bug in
    /// ContentPresenter.ApplyTextStyle) - so it takes the state colour by inheritance, and a blanket style masks it.
    /// This is the whole surface of the fragility, and it is worth knowing it is this narrow.</summary>
    [Test]
    public void AuthoredTextContent_IsTheHalfAStyleCanBreak()
    {
        var authored = new TextBlock { Text = "row" };
        var presenter = new ContentPresenter { Content = authored, Foreground = Brushes.Red };
        presenter.Measure(new Size(120, 30));
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(presenter);

        TestContext.WriteLine($"authored TextBlock before any style={authored.Foreground}");
        Assert.That(authored.Foreground, Is.SameAs(Brushes.Red), "it follows the presenter by INHERITANCE");

        authored.SetValue(TextBlock.ForegroundProperty, Brushes.Blue, ValuePriority.Style);

        TestContext.WriteLine($"authored TextBlock after a Style-priority setter={authored.Foreground}");
        Assert.That(authored.Foreground, Is.SameAs(Brushes.Blue), "...and a blanket style cuts that inheritance");
    }
}
