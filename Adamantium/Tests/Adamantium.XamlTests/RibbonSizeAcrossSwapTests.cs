using System.Linq;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// A ribbon command wears one of three sizes, and which one is said by TRIGGERS that address the template's parts by
/// name. A theme swap rebuilds those parts, so the triggers have to be re-pointed at the new ones - and a trigger left
/// pointing at the discarded template leaves the new one holding whatever its markup happened to say, which for a
/// label collapsed by the Small layout is nothing at all.
/// </summary>
[TestFixture]
public class RibbonSizeAcrossSwapTests
{
    private FakeApp _app;

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
        var themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = themes;
        ((FakeContext)_app.UIContext).ThemeEngine = themes;
        var fluent = new Adamantium.UI.Themes.FluentTheme.Fluent();
        themes.AddTheme(fluent.Name, fluent);
        themes.SetTheme(fluent);
    }

    private static void Lay(MeasurableUIComponent c)
    {
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(c);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        c.Measure(new Size(300, 120));
        c.Arrange(new Rect(0, 0, 300, 120));
    }

    private static string Describe(RibbonButton button)
    {
        var icon = button.GetTemplateChild("PART_Icon") as UIComponent;
        var label = button.GetTemplateChild("PART_ContentPresenter") as UIComponent;
        var box = button.GetTemplateChild("IconBox") as UIComponent;
        return $"icon={icon?.Visibility}/{icon?.RenderSize} label={label?.Visibility}/{label?.RenderSize} " +
               $"box={box?.RenderSize} button={button.RenderSize}";
    }

    [TestCase(RibbonSize.Large)]
    [TestCase(RibbonSize.Medium)]
    [TestCase(RibbonSize.Small)]
    public void ACommandKeepsItsSizeLayoutAcrossAThemeSwap(RibbonSize size)
    {
        var button = new RibbonButton { Content = "Align left" };
        Ribbon.SetSize(button, size);
        button.ApplyCurrentTheme();
        Lay(button);

        var before = Describe(button);
        TestContext.WriteLine($"{size} before: {before}");

        var macOs = new Adamantium.UI.Themes.MacOsTheme.MacOs();
        _app.ThemeManager.AddTheme(macOs.Name, macOs);
        _app.ThemeManager.SetTheme(macOs);
        Lay(button);

        var after = Describe(button);
        TestContext.WriteLine($"{size} after:  {after}");

        Assert.That(button.RenderSize.Width, Is.GreaterThan(0), "the command still takes room");
        Assert.That(button.RenderSize.Height, Is.GreaterThan(0), "the command still takes room");

        var label = button.GetTemplateChild("PART_ContentPresenter") as UIComponent;
        if (size != RibbonSize.Small)
            Assert.That(label?.Visibility, Is.EqualTo(Visibility.Visible),
                "everything but Small shows its label, before the swap and after it");
    }

    // A swap changes what everything DRAWS - new fills, new strokes, new radii - and the recorder re-records only what
    // says its recorded geometry is stale. A part that never says so is one nothing will ever ask to draw again: it
    // keeps the picture it had, which on screen is a control you can still hover and still press, showing nothing.
    // Asked through the invalidation NOTIFICATION rather than the flag, because the flag is cleared by a record pass
    // and there is no recorder here - what matters is whether the swap raised the event at all.
    [Test]
    public void ARestyledPartAsksToBeDrawnAgain()
    {
        var button = new RibbonButton { Content = "Align left" };
        Ribbon.SetSize(button, RibbonSize.Medium);
        button.ApplyCurrentTheme();
        Lay(button);

        var chromeBefore = button.GetTemplateChild("Chrome") as Adamantium.UI.Controls.Decorators.Border;
        TestContext.WriteLine($"before: chromeRadius={chromeBefore?.CornerRadius} chromeBg={chromeBefore?.Background} " +
                              $"buttonRadius={button.CornerRadius} buttonPadding={button.Padding}");

        var geometry = new System.Collections.Generic.List<IUIComponent>();
        var paint = new System.Collections.Generic.List<IUIComponent>();
        void WatchGeometry(IUIComponent c) => geometry.Add(c);
        void WatchPaint(IUIComponent c) => paint.Add(c);
        Adamantium.UI.Core.VisualTreeNotifications.ContentInvalidated += WatchGeometry;
        Adamantium.UI.Core.VisualTreeNotifications.PaintInvalidated += WatchPaint;

        var macOs = new Adamantium.UI.Themes.MacOsTheme.MacOs();
        _app.ThemeManager.AddTheme(macOs.Name, macOs);
        _app.ThemeManager.SetTheme(macOs);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        Adamantium.UI.Core.VisualTreeNotifications.ContentInvalidated -= WatchGeometry;
        Adamantium.UI.Core.VisualTreeNotifications.PaintInvalidated -= WatchPaint;

        var chrome = button.GetTemplateChild("Chrome") as UIComponent;
        var label = button.GetTemplateChild("PART_ContentPresenter") as UIComponent;
        var chromeAfter = button.GetTemplateChild("Chrome") as Adamantium.UI.Controls.Decorators.Border;
        TestContext.WriteLine($"after:  chromeRadius={chromeAfter?.CornerRadius} chromeBg={chromeAfter?.Background} " +
                              $"buttonRadius={button.CornerRadius} buttonPadding={button.Padding}");
        TestContext.WriteLine($"same chrome instance={ReferenceEquals(chromeBefore, chromeAfter)}");
        TestContext.WriteLine($"geometry={geometry.Count} paint={paint.Count} during the swap");
        foreach (var c in geometry.Distinct().Take(10)) TestContext.WriteLine($"  geom {c.GetType().Name}");
        foreach (var c in paint.Distinct().Take(10)) TestContext.WriteLine($"  paint {c.GetType().Name}");
        TestContext.WriteLine($"chrome in geometry={geometry.Contains(chrome)} in paint={paint.Contains(chrome)}");
        TestContext.WriteLine($"label  in geometry={geometry.Contains(label)} in paint={paint.Contains(label)}");

        Assert.That(geometry.Contains(chrome) || paint.Contains(chrome), Is.True,
            "the part carrying the theme's fill has to say its picture is stale, on one channel or the other");
    }

    private static string Ink(object component)
    {
        if (component is not IAdamantiumComponent c) return "<no component>";
        var brush = c.GetValue(c.GetProperty("Foreground"));
        return brush switch
        {
            null => "<null>",
            Adamantium.UI.Core.Media.SolidColorBrush solid => $"A:{solid.Color.A} R:{solid.Color.R} G:{solid.Color.G} B:{solid.Color.B}",
            _ => brush.ToString()
        };
    }

    private static byte Alpha(object component)
    {
        if (component is not IAdamantiumComponent c) return 0;
        return c.GetValue(c.GetProperty("Foreground")) is Adamantium.UI.Core.Media.SolidColorBrush solid ? solid.Color.A : (byte)0;
    }

    /// <summary>
    /// A command's icon is DATA: the shape comes from IconTemplate and the colour from the presenter's Foreground,
    /// which a template binding takes from the button. So a button whose Foreground is transparent after a theme swap
    /// is a button you can still hover and still press, drawn at its right size, showing nothing at all.
    /// </summary>
    [Test]
    public void ACommandKeepsItsInkAcrossAThemeSwap()
    {
        var button = new RibbonButton { Content = "Align left" };
        Ribbon.SetSize(button, RibbonSize.Medium);
        button.ApplyCurrentTheme();
        Lay(button);

        TestContext.WriteLine($"before: button={Ink(button)} icon={Ink(button.GetTemplateChild("PART_Icon"))} " +
                              $"label={Ink(button.GetTemplateChild("PART_ContentPresenter"))}");

        var macOs = new Adamantium.UI.Themes.MacOsTheme.MacOs();
        _app.ThemeManager.AddTheme(macOs.Name, macOs);
        _app.ThemeManager.SetTheme(macOs);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        Lay(button);

        TestContext.WriteLine($"after:  button={Ink(button)} icon={Ink(button.GetTemplateChild("PART_Icon"))} " +
                              $"label={Ink(button.GetTemplateChild("PART_ContentPresenter"))}");

        Assert.That(Alpha(button), Is.GreaterThan(0), "the command still has ink of its own after the swap");
        Assert.That(Alpha(button.GetTemplateChild("PART_Icon")), Is.GreaterThan(0), "and its icon presenter carries it");
    }
}


