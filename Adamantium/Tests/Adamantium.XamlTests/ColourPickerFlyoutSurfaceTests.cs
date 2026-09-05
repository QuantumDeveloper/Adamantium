using System;
using System.IO;
using System.Text.RegularExpressions;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// A colour picker is two different things depending on where it stands: a solid panel on a page, and a FLYOUT when a
/// colour well opens it over the document. Only the second is a transient surface, and a transient surface in a theme
/// that has a material wears it - the menus, the drop-downs and the slide panel all do. This one did not, in either
/// theme: it kept a flat card while everything else around it was glass or acrylic.
/// <para>Editor Pro is deliberately absent: it has no materials at all, and its menus are flat by design.</para>
/// </summary>
[TestFixture]
public class ColourPickerFlyoutSurfaceTests
{
    private FakeApp _app;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    private ColorPicker BuiltUnder(ITheme theme)
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);
        var themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = themes;
        ((FakeContext)_app.UIContext).ThemeEngine = themes;
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);

        var picker = new ColorPicker();
        picker.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(picker);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        picker.Measure(new Size(700, 400));
        picker.Arrange(new Rect(0, 0, 700, 400));
        return picker;
    }

    private static ITheme ThemeNamed(string name) => name switch
    {
        "macOS" => new Adamantium.UI.Themes.MacOsTheme.MacOs(),
        _ => new Adamantium.UI.Themes.FluentTheme.Fluent()
    };

    // The seam the flyout needs: the surface follows the control's own Background, so the ONE place that knows where
    // the picker is standing - the colour well's popup - can hand it a material. Baked into the template as a fixed
    // resource, as it was, no caller can say anything about it at all.
    [TestCase("Fluent")]
    [TestCase("macOS")]
    public void ThePickersSurfaceFollowsItsBackground(string themeName)
    {
        var picker = BuiltUnder(ThemeNamed(themeName));
        var surface = picker.GetTemplateChild("PickerSurface") as Border;

        Assert.That(surface, Is.Not.Null, "the picker's surface has to be nameable to be answerable");

        var standalone = surface.Background;
        TestContext.WriteLine($"{themeName}: standalone surface = {standalone}");
        Assert.That(standalone, Is.SameAs(picker.Background), "a page's picker wears what the style gives it");

        var material = new SolidColorBrush(Colors.Magenta);
        picker.Background = material;
        Assert.That(surface.Background, Is.SameAs(material),
            "...and a caller that knows better can replace it - which is what the flyout does");
    }

    // ...and that the flyout actually does it. Textual, because the picker inside a colour well is built lazily on the
    // popup's first open and there is nothing to inspect until someone clicks.
    [TestCase("FluentTheme")]
    [TestCase("MacOsTheme")]
    public void TheColourWellsPopupHandsThePickerTheFlyoutMaterial(string themeFolder)
    {
        var file = Directory.GetFiles(Path.Combine(ThemesRoot(), themeFolder), "*ColorPickerButtonStyleSet.auml");
        Assert.That(file, Has.Length.EqualTo(1), "the theme owns exactly one colour-well style set");

        var markup = File.ReadAllText(file[0]);
        var picker = Regex.Match(markup, @"<ColorPicker\b[^>]*>", RegexOptions.Singleline);

        Assert.That(picker.Success, Is.True, "the popup builds a ColorPicker");
        Assert.That(picker.Value, Does.Contain("FlyoutSurfaceFill"),
            "a panel that appears over the document for the length of a choice is a flyout, and wears the flyout material");
    }

    private static string ThemesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Adamantium.UI.Themes");
            if (Directory.Exists(Path.Combine(candidate, "FluentTheme"))) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("could not find Adamantium.UI.Themes above " + AppContext.BaseDirectory);
    }
}
