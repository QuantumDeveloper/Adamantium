using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>A divider RUNS one way or the other. Between the rows of a menu it lies across them; between two groups of
/// buttons standing side by side it stands on end - and a rule with no length in the direction it was put is a rule
/// nobody can see, which is what a horizontal one does in a row.</summary>
[TestFixture]
public class SeparatorThemeTests
{
    private FakeApp _app;
    private ThemeManager _themes;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    private void Use(Theme theme)
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);
        _themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = _themes;
        ((FakeContext)_app.UIContext).ThemeEngine = _themes;

        _themes.AddTheme(theme.Name, theme);
        _themes.SetTheme(theme);
    }

    private static Theme ThemeNamed(string name) => name switch
    {
        "MacOs" => new Adamantium.UI.Themes.MacOsTheme.MacOs(),
        "EditorPro" => new Adamantium.UI.Themes.EditorProTheme.EditorPro(),
        _ => new Adamantium.UI.Themes.FluentTheme.Fluent()
    };

    private static Separator Built(Separator separator)
    {
        separator.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(separator);
        separator.Measure(new Size(200, 40));
        separator.Arrange(new Rect(0, 0, 200, 40));
        return separator;
    }

    // The RULE inside, and not the control: the control stretches to whatever it was given, and the question is what
    // got drawn in it.
    private static Rect Rule(Separator separator) =>
        (separator.GetTemplateChild("PART_Rule") as IUIComponent)?.Bounds ?? default;

    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void ItLiesAcrossTheRowsByDefault(string theme)
    {
        Use(ThemeNamed(theme));
        var rule = Rule(Built(new Separator()));

        Assert.That(new Separator().Orientation, Is.EqualTo(Orientation.Horizontal));
        Assert.That(rule.Height, Is.GreaterThan(0).And.LessThan(4), "a rule across a menu is a hairline, not a band");
        Assert.That(rule.Width, Is.GreaterThan(100), "...and it runs the width of what holds it");
    }

    // A rule between two groups in a ROW: it must have width to be drawn at all, and height to be worth drawing. A
    // horizontal one put in a row has neither - it is a hairline stretched across nothing, and nobody sees it.
    [Test]
    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void ItStandsOnEndWhenAsked(string theme)
    {
        Use(ThemeNamed(theme));
        var rule = Rule(Built(new Separator { Orientation = Orientation.Vertical }));

        Assert.That(rule.Width, Is.GreaterThan(0).And.LessThan(4),
            "a rule down a toolbar is a hairline, not a column");
        Assert.That(rule.Height, Is.GreaterThan(20), "...and it runs the height of what holds it");
    }
}
