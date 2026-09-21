using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Themes.MacOsTheme;
using NUnit.Framework;

namespace Adamantium.XamlTests;

[TestFixture]
public class MacOsCheckBoxFillTests
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

    [Test]
    public void TheUncheckedBoxIsADifferentColourInEachVariant()
    {
        var theme = new MacOs();
        _themes.AddTheme(theme.Name, theme);
        _themes.SetTheme(theme);

        var dark = FillUnder(theme, ThemeVariant.Dark);
        var light = FillUnder(theme, ThemeVariant.Light);

        TestContext.WriteLine($"dark  = {dark}");
        TestContext.WriteLine($"light = {light}");

        Assert.That(light, Is.Not.EqualTo(dark), "the two variants must not answer the same colour for the box");
    }

    [Test]
    public void AnExistingBoxFollowsTheVariantSwitch()
    {
        var theme = new MacOs();
        _themes.AddTheme(theme.Name, theme);
        _themes.SetTheme(theme);
        theme.ApplyVariant(ThemeVariant.Dark);

        var box = new CheckBox();
        box.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(box);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        box.Measure(new Size(120, 24));

        var before = (box.Background as SolidColorBrush)?.Color;

        theme.ApplyVariant(ThemeVariant.Light);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        box.Measure(new Size(120, 24));

        var after = (box.Background as SolidColorBrush)?.Color;

        TestContext.WriteLine($"the SAME box: dark -> {before}, after switching to light -> {after}");
        Assert.That(after, Is.Not.EqualTo(before), "a live variant switch has to reach a box that already exists");
    }

    private Color FillUnder(MacOs theme, ThemeVariant variant)
    {
        Assert.That(theme.ApplyVariant(variant), Is.True, $"the theme accepted the {variant} variant");

        var box = new CheckBox();
        box.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(box);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        box.Measure(new Size(120, 24));

        var brush = box.Background as SolidColorBrush;
        Assert.That(brush, Is.Not.Null, $"the box has a solid fill under {variant}");
        return brush.Color;
    }
}
