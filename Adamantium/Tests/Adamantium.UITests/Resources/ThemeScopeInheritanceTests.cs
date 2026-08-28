using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.UITests.Resources;

/// <summary>
/// A theme scope is declared on ONE element and has to hold for everything under it - that is what makes it a scope.
/// <para>Written after a stand showed a pane resolving its own BACKGROUND from its scope while the text inside it
/// resolved from the application's: the background sits on the very element that carries the scope, so it reads a
/// LOCAL value, and only the text had to inherit one.</para>
/// </summary>
[TestFixture]
public class ThemeScopeInheritanceTests
{
    [Test]
    public void AScopeDeclaredOnAnAncestor_ReachesTheElementsUnderIt()
    {
        var text = new TextBlock { Text = "Hello" };
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(text);

        var scopeRoot = new Border { Child = panel };
        ThemeContext.SetVariant(scopeRoot, ThemeVariant.Light);

        Assert.That(ThemeContext.GetVariant(scopeRoot), Is.EqualTo(ThemeVariant.Light),
            "the element that DECLARES the scope reads it locally - this half always worked");

        Assert.That(ThemeContext.GetVariant(panel), Is.EqualTo(ThemeVariant.Light),
            "a panel one level down has to inherit it");

        Assert.That(ThemeContext.GetVariant(text), Is.EqualTo(ThemeVariant.Light),
            "and so does the text, which is the whole point: a scope covers a SUBTREE, not one element");
    }

    /// <summary>The order an application actually does it in: the subtree is built and READ first (a resource lookup
    /// per element, at attach), and the scope arrives afterwards - it is bound, so it lands when the binding produces a
    /// value. An inherited value is cached against an epoch, so this is the case where a descendant can be left holding
    /// the answer it resolved before the scope existed.</summary>
    [Test]
    public void AScopeThatARRIVESLater_ReachesElementsThatWereAlreadyRead()
    {
        var text = new TextBlock { Text = "Hello" };
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(text);
        var scopeRoot = new Border { Child = panel };

        // Read BEFORE the scope is declared - this is what fills the inherited cache with "no scope".
        Assume.That(ThemeContext.GetVariant(text).IsUnspecified, Is.True, "precondition: no scope yet");

        ThemeContext.SetVariant(scopeRoot, ThemeVariant.Light);

        Assert.That(ThemeContext.GetVariant(text), Is.EqualTo(ThemeVariant.Light),
            "a scope that arrives after the subtree was read still has to reach it");
    }
}
