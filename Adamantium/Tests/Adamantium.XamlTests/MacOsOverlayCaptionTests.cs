using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Themes.MacOsTheme;
using NUnit.Framework;

namespace Adamantium.XamlTests;

[TestFixture]
public class MacOsOverlayCaptionTests
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

        var theme = new MacOs();
        _themes.AddTheme(theme.Name, theme);
        _themes.SetTheme(theme);
    }

    private static OverlayWindow Built(bool canPin)
    {
        var window = new OverlayWindow { Title = "probe", CanPin = canPin, CanClose = true };
        window.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        window.Measure(new Size(400, 200));
        window.Arrange(new Rect(0, 0, 400, 200));
        return window;
    }

    [Test]
    public void WithCanPin_ThePinLightIsThere()
    {
        var window = Built(canPin: true);

        var pin = window.GetTemplateChild("PART_PinButton") as IUIComponent;
        var close = window.GetTemplateChild("PART_CloseButton") as IUIComponent;

        TestContext.WriteLine($"close = {close?.GetType().Name}, visibility={(close as UIComponent)?.Visibility}, bounds={close?.Bounds}");
        TestContext.WriteLine($"pin   = {pin?.GetType().Name}, visibility={(pin as UIComponent)?.Visibility}, bounds={pin?.Bounds}");

        Assert.Multiple(() =>
        {
            Assert.That(close, Is.Not.Null, "the caption has a close light");
            Assert.That(pin, Is.Not.Null, "the caption has a pin light");
            Assert.That((pin as UIComponent)?.Visibility, Is.EqualTo(Visibility.Visible),
                "CanPin=true must reveal the pin light");
            Assert.That(pin.Bounds.Width, Is.GreaterThan(0), "and it must take real space, not collapse to nothing");
        });
    }

    [Test]
    public void WithoutCanPin_ThePinLightIsHidden()
    {
        var window = Built(canPin: false);

        // The CELL is what hides, not the button: the light and its glyph sit in one cell, and hiding the button alone
        // would leave the glyph floating over an empty space.
        var cell = window.GetTemplateChild("PinCell") as UIComponent;

        TestContext.WriteLine($"pin cell without CanPin -> visibility={cell?.Visibility}");
        Assert.That(cell, Is.Not.Null, "the caption has a pin cell");
        Assert.That(cell.Visibility, Is.Not.EqualTo(Visibility.Visible));
    }
}
