using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>What size a plain <see cref="TextBlock"/> in a view actually comes out at, and where that number comes
/// from - measured because the answer was reasoned out twice and got wrong both times.
/// <para>Three numbers are in play and they are in three different places: UIComponent's FontSize default (14),
/// TextBlock's own override of it (12, in C#), and the theme's FontSizeBody (14 in Fluent and macOS, 12 in Editor Pro).
/// Which of them a label wears is decided by the priority ladder, where INHERITED outranks DEFAULT - so an ancestor's
/// effective value, even one it holds only by default, beats the element's own default.</para>
/// </summary>
[TestFixture]
public class LooseTextSizeTests
{
    /// <summary>An ancestor holding its size only BY DEFAULT does not hand it down - the label keeps its own default
    /// instead. This is the surprising half, and the reason a theme cannot leave the seeding implicit: putting a
    /// container above the text is not enough, the size has to be SET somewhere.</summary>
    [Test]
    public void AnAncestorsDEFAULT_DoesNotReachTheLabel()
    {
        var text = new TextBlock { Text = "label" };
        var root = new Border { Width = 200, Height = 60, Child = text };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);

        TestContext.WriteLine($"ancestor FontSize={root.FontSize} (its default), TextBlock FontSize={text.FontSize}");

        Assert.That(root.FontSize, Is.EqualTo(14), "the ancestor is at UIComponent's default");
        Assert.That(text.FontSize, Is.EqualTo(12),
            "...and the label is at its OWN default, not the ancestor's - only a value that was SET is inherited");
    }

    /// <summary>...and an ancestor that says a size explicitly is followed too. This is the whole mechanism a theme
    /// would use to give loose text its body size: seed it once, high up, and let it fall.</summary>
    [Test]
    public void AnAncestorThatSetsASize_IsFollowed()
    {
        var text = new TextBlock { Text = "label" };
        var root = new Border { Width = 200, Height = 60, FontSize = 20, Child = text };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);

        TestContext.WriteLine($"ancestor FontSize=20, TextBlock FontSize={text.FontSize}");
        Assert.That(text.FontSize, Is.EqualTo(20));
    }

    /// <summary>With NO ancestor at all, the element's own default is what is left - which is where TextBlock's 12
    /// actually applies, and the only place it does.</summary>
    [Test]
    public void WithNoAncestor_ItsOwnDefaultApplies()
    {
        var text = new TextBlock { Text = "label" };
        TestContext.WriteLine($"orphan TextBlock FontSize={text.FontSize}");

        Assert.That(text.FontSize, Is.EqualTo(12));
    }
}
