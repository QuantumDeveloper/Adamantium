using System.Linq;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// A tab says which one is CURRENT with two marks: the plate behind it and the colour of its label. The plate is a
/// trigger writing a part's Background and has always worked; the label is the same trigger writing the part's
/// Foreground and did not - the strip highlighted correctly while every label stayed the resting colour, and the one
/// tab that happened to be selected when the theme was applied kept the selected colour for good.
/// <para>What makes the label different from the plate is that nothing paints it directly: the presenter's Foreground
/// has to reach the TextBlock the presenter GENERATES for a string header, and that hand-off is the thing under
/// test.</para>
/// </summary>
[TestFixture]
public class TabForegroundFollowsSelectionTests
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
    }

    private void Use(ITheme theme)
    {
        _themes.AddTheme(theme.Name, theme);
        _themes.SetTheme(theme);
    }

    private static ITheme Build(string name) => name == "EditorPro"
        ? new Adamantium.UI.Themes.EditorProTheme.EditorPro()
        : new Adamantium.UI.Themes.FluentTheme.Fluent();

    // A themed tab with its template built and its label generated - the generated TextBlock is made in the presenter's
    // MEASURE, so nothing here exists until the tab has been measured once.
    private static TabItem LiveTab()
    {
        var tab = new TabItem { Header = "Shapes" };
        tab.ApplyCurrentTheme();
        Frame(tab);
        return tab;
    }

    // What a frame does, to the extent this seam depends on it: drain the coalesced binding updates, then lay out.
    private static void Frame(TabItem tab)
    {
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        tab.Measure(new Size(200, 32));
    }

    private static ContentPresenter PresenterOf(TabItem tab)
    {
        var presenter = tab.GetTemplateChild("PART_ContentPresenter") as ContentPresenter;
        Assert.That(presenter, Is.Not.Null, "the tab template has no PART_ContentPresenter");
        return presenter;
    }

    private static TextBlock LabelOf(TabItem tab) =>
        PresenterOf(tab).VisualChildren.OfType<TextBlock>().FirstOrDefault();

    private static Color ColourOf(Brush brush) =>
        brush is SolidColorBrush solid ? solid.Color : default;

    /// <summary>The first link: the TRIGGER onto the part. If this one holds and the label still does not change, the
    /// defect is in the hand-off from the presenter to the text it generated, not in the theme.</summary>
    [TestCase("EditorPro")]
    [TestCase("Fluent")]
    public void SelectingATab_ChangesThePresentersForeground(string themeName)
    {
        Use(Build(themeName));
        var tab = LiveTab();
        var presenter = PresenterOf(tab);
        var resting = ColourOf(presenter.Foreground);

        tab.IsSelected = true;

        Assert.That(ColourOf(presenter.Foreground), Is.Not.EqualTo(resting),
            "the IsSelected trigger never reached PART_ContentPresenter");
    }

    [TestCase("EditorPro")]
    [TestCase("Fluent")]
    public void SelectingATab_ChangesItsLabelColour(string themeName)
    {
        Use(Build(themeName));
        var tab = LiveTab();
        var label = LabelOf(tab);
        Assert.That(label, Is.Not.Null, "the header string never became a TextBlock");
        var resting = ColourOf(label.Foreground);

        tab.IsSelected = true;
        Frame(tab);

        // Re-read rather than trust the reference taken above: a presenter is free to REBUILD its generated text, and
        // asserting on the old object would report a stale colour as a defect.
        Assert.That(ColourOf(LabelOf(tab).Foreground), Is.Not.EqualTo(resting),
            "the selected tab's label kept the resting colour - the plate says 'current' and the text does not");
    }

    [TestCase("EditorPro")]
    [TestCase("Fluent")]
    public void DeselectingATab_PutsItsLabelColourBack(string themeName)
    {
        Use(Build(themeName));
        var tab = LiveTab();
        var label = LabelOf(tab);
        var resting = ColourOf(label.Foreground);

        tab.IsSelected = true;
        Frame(tab);
        tab.IsSelected = false;
        Frame(tab);

        Assert.That(ColourOf(LabelOf(tab).Foreground), Is.EqualTo(resting),
            "the label stayed in the selected colour after the tab lost selection - it sticks on whichever tab was " +
            "current when the colour was first written");
    }

    /// <summary>The seam itself, with no theme in sight: a presenter's own Foreground reaching the text it generated.
    /// Everything a theme does to a label - selected, hovered, disabled, accent - arrives through exactly this.</summary>
    [Test]
    public void AGeneratedLabelFollowsThePresentersForeground()
    {
        var red = Color.FromRgba(220, 40, 40, 255);
        var green = Color.FromRgba(40, 200, 90, 255);
        var presenter = new ContentPresenter { Content = "Shapes", Foreground = new SolidColorBrush(red) };
        presenter.Measure(new Size(200, 32));
        var label = presenter.VisualChildren.OfType<TextBlock>().FirstOrDefault();
        Assert.That(label, Is.Not.Null, "the string content never became a TextBlock");
        Assume.That(ColourOf(label.Foreground), Is.EqualTo(red), "precondition: the label starts in the presenter's colour");

        presenter.Foreground = new SolidColorBrush(green);
        // A source change on an ELEMENT source is coalesced into the per-frame binding queue, so a test has to stand in
        // for the frame that would drain it.
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        presenter.Measure(new Size(200, 32));

        Assert.That(ColourOf(presenter.VisualChildren.OfType<TextBlock>().First().Foreground), Is.EqualTo(green),
            "the label kept the colour the presenter held when the text was built");
    }

    /// <summary>The same seam for TEMPLATED content - the shape the stand actually uses, where a tab header comes from an
    /// ItemTemplate holding an AUTHORED TextBlock. The presenter deliberately never writes into one of those (an explicit
    /// write would outrank inheritance for good), so the colour has to arrive by INHERITANCE, and it has to arrive again
    /// on every later change.</summary>
    [Test]
    public void ATemplatedLabelFollowsThePresentersForeground()
    {
        var red = Color.FromRgba(220, 40, 40, 255);
        var green = Color.FromRgba(40, 200, 90, 255);
        var presenter = new ContentPresenter
        {
            Content = "Shapes",
            Foreground = new SolidColorBrush(red),
            ContentTemplate = new Adamantium.UI.Core.Templates.DataTemplate(() =>
            {
                var text = new TextBlock { Text = "Shapes" };
                return new Adamantium.UI.Core.Templates.TemplateResult { RootComponent = text };
            })
        };
        presenter.Measure(new Size(200, 32));
        var label = presenter.VisualChildren.OfType<TextBlock>().FirstOrDefault();
        Assert.That(label, Is.Not.Null, "the content template never produced a TextBlock");
        Assume.That(ColourOf(label.Foreground), Is.EqualTo(red), "precondition: the label starts in the presenter's colour");

        presenter.Foreground = new SolidColorBrush(green);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        presenter.Measure(new Size(200, 32));

        Assert.That(ColourOf(presenter.VisualChildren.OfType<TextBlock>().First().Foreground), Is.EqualTo(green),
            "a templated label froze on the colour the presenter held when the template was built");
    }

    /// <summary>The stand's actual shape: a tab whose header comes from a template, so the label is an AUTHORED
    /// TextBlock reached by inheritance - and the colour is written by a TRIGGER rather than by hand. Each half of that
    /// works on its own; this is the pair.</summary>
    [TestCase("EditorPro")]
    [TestCase("Fluent")]
    public void SelectingATab_ChangesATEMPLATEDLabelsColour(string themeName)
    {
        Use(Build(themeName));
        var tab = new TabItem
        {
            Header = "Shapes",
            HeaderTemplate = new Adamantium.UI.Core.Templates.DataTemplate(() =>
                new Adamantium.UI.Core.Templates.TemplateResult { RootComponent = new TextBlock { Text = "Shapes" } })
        };
        tab.ApplyCurrentTheme();
        Frame(tab);
        var resting = ColourOf(LabelOf(tab).Foreground);

        tab.IsSelected = true;
        Frame(tab);

        Assert.That(ColourOf(LabelOf(tab).Foreground), Is.Not.EqualTo(resting),
            "a templated tab label ignored the selection - the trigger wrote the presenter and the text never heard");
    }

    /// <summary>The report that found this: start in one theme, switch to the other, then click around the strip.
    /// A theme change rebuilds the template, so the trigger has to land on the NEW parts.</summary>
    [Test]
    public void AfterAThemeChange_TheLabelStillFollowsSelection()
    {
        Use(Build("Fluent"));
        var tab = LiveTab();
        tab.IsSelected = true;
        Frame(tab);

        Use(Build("EditorPro"));
        tab.ApplyCurrentTheme();
        Frame(tab);

        var selected = ColourOf(LabelOf(tab).Foreground);

        tab.IsSelected = false;
        Frame(tab);

        Assert.That(ColourOf(LabelOf(tab).Foreground), Is.Not.EqualTo(selected),
            "after the theme changed, the label froze on the colour it had at the moment of the change");
    }
}
