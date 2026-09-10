using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// The Expander under each theme. Two things are asked of every one of them: the parts the control drives by name are
/// there, and a FOLDED section costs nothing - the content host is collapsed rather than merely invisible, because a
/// hidden element is still measured and a page of thirty folded sections would build thirty pages for nothing.
/// </summary>
[TestFixture]
public class ExpanderThemeTests
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

    private static Expander Built(Expander expander)
    {
        expander.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(expander);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        expander.Measure(new Size(400, 400));
        expander.Arrange(new Rect(0, 0, 400, 400));
        return expander;
    }

    private static Theme MacOs() => new Adamantium.UI.Themes.MacOsTheme.MacOs();

    private static Theme Fluent() => new Adamantium.UI.Themes.FluentTheme.Fluent();

    private static Theme EditorPro() => new Adamantium.UI.Themes.EditorProTheme.EditorPro();

    [Test]
    public void ItKeepsItsPartsUnderMacOs() => Parts(MacOs());

    [Test]
    public void ItKeepsItsPartsUnderFluent() => Parts(Fluent());

    [Test]
    public void ItKeepsItsPartsUnderEditorPro() => Parts(EditorPro());

    private void Parts(Theme theme)
    {
        Use(theme);
        var expander = Built(new Expander { Header = "Transform", Content = "body" });

        Assert.Multiple(() =>
        {
            Assert.That(expander.Template, Is.Not.Null, "a template that threw leaves the control with none at all");
            Assert.That(expander.GetTemplateChild("PART_Header"), Is.Not.Null, "the whole header is the button");
            Assert.That(expander.GetTemplateChild("PART_Content"), Is.Not.Null, "and the host that folds away");
        });
    }

    [Test]
    public void AFoldedSectionCostsNothingUnderMacOs() => FoldedCostsNothing(MacOs());

    [Test]
    public void AFoldedSectionCostsNothingUnderFluent() => FoldedCostsNothing(Fluent());

    [Test]
    public void AFoldedSectionCostsNothingUnderEditorPro() => FoldedCostsNothing(EditorPro());

    private void FoldedCostsNothing(Theme theme)
    {
        Use(theme);

        var body = new Adamantium.UI.Controls.Decorators.Border { Width = 200, Height = 150 };
        var expander = Built(new Expander { Header = "Transform", Content = body });

        var folded = expander.DesiredSize.Height;

        Assert.Multiple(() =>
        {
            Assert.That(((IUIComponent)expander.GetTemplateChild("PART_Content")).Visibility,
                Is.EqualTo(Visibility.Collapsed), "COLLAPSED, not hidden - a hidden host is still measured");
            Assert.That(folded, Is.LessThan(150), $"folded, the section is a header ({folded}), not a header plus a body");
        });

        expander.IsExpanded = true;

        // A child's Visibility change does not dirty the chain above it in this engine (the LayoutManager drains such a
        // node directly in the app), so the walk is dirtied by hand - as everywhere else these tests re-lay a subtree.
        for (IUIComponent node = (IUIComponent)expander.GetTemplateChild("PART_Content"); node != null; node = node.VisualParent)
            (node as IMeasurableComponent)?.InvalidateMeasure();

        expander.Measure(new Size(400, 400), force: true);
        expander.Arrange(new Rect(0, 0, 400, 400));

        Assert.Multiple(() =>
        {
            Assert.That(((IUIComponent)expander.GetTemplateChild("PART_Content")).Visibility,
                Is.EqualTo(Visibility.Visible));
            Assert.That(expander.DesiredSize.Height, Is.GreaterThan(folded + 100), "opened, the body is in the size");
        });
    }
}
