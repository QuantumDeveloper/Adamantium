using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>The pictures the canvas tools name. A tool says a KEY and the theme answers with a picture, so a key the
/// theme has never heard of is a button with nothing on it - and nothing on it is exactly what an icon-only rail looks
/// like when it is working, which is why this is worth asking rather than looking at.</summary>
[TestFixture]
public class CanvasIconTests
{
    // Every key a tool in this engine names, plus the plus. Written out on purpose: the point is to catch a tool that
    // names a key nobody added and a key that gets renamed out from under a tool, and a list generated from the same
    // place as the icons would catch neither.
    private static readonly string[] Keys =
    {
        "ToolSelectIcon", "ToolPenIcon", "ToolTextIcon", "ToolEraseIcon", "ToolEraseStrokeIcon",
        "ToolRectangleIcon", "ToolEllipseIcon", "ToolPolygonIcon", "ToolLineIcon", "ToolArrowIcon", "ToolCurveIcon",
        "ToolButtonIcon", "ToolCheckBoxIcon", "ToolTextBoxIcon", "ToolNodeIcon", "ToolAddIcon"
    };

    private FakeApp _app;

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

        var themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = themes;
        ((FakeContext)_app.UIContext).ThemeEngine = themes;

        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);
    }

    private static Theme ThemeNamed(string name) => name switch
    {
        "MacOs" => new Adamantium.UI.Themes.MacOsTheme.MacOs(),
        "EditorPro" => new Adamantium.UI.Themes.EditorProTheme.EditorPro(),
        _ => new Adamantium.UI.Themes.FluentTheme.Fluent()
    };

    [TestCase("Fluent")]
    [TestCase("EditorPro")]
    [TestCase("MacOs")]
    public void EveryToolPictureIsThere(string theme)
    {
        Use(ThemeNamed(theme));

        Assert.Multiple(() =>
        {
            foreach (var key in Keys)
            {
                Assert.That(_app.ResourceManager.FindResource(key), Is.Not.Null, key);
            }
        });
    }
}
