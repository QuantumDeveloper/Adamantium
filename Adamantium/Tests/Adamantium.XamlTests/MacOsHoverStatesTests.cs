using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Themes.MacOsTheme;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// Hover in this theme is OPT-IN PER CLASS. The plain button and the plain toggle carry no IsMouseOver trigger at all -
/// a deliberate decision, written beside them: a bordered control here does not light up. The consequence is that every
/// FLAT GLYPH CHIP has to say so for itself, and one that forgets names a BackgroundPointerOver brush nothing ever
/// reads. Nothing complains: the chip is the right size, it is clickable, it presses - it simply never answers the
/// pointer, which is the whole of what makes a glyph look like a control rather than an ornament.
/// </summary>
[TestFixture]
public class MacOsHoverStatesTests
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

        var theme = new MacOs();
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);
    }

    private static void Lights(ButtonBase chip, string className)
    {
        chip.ClassNames.Add(className);
        chip.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(chip);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        chip.Measure(new Size(40, 30));
        chip.Arrange(new Rect(0, 0, 40, 30));

        var plate = chip.GetTemplateChild("InnerBorder") as Border;
        Assert.That(plate, Is.Not.Null, $"{className}: the part the hover fill is written to");

        var atRest = plate.Background;
        chip.SetValue(InputUIComponent.IsMouseOverProperty, true);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();

        TestContext.WriteLine($"{className}: rest={atRest} hover={plate.Background} declared={chip.BackgroundPointerOver}");

        Assert.That(chip.BackgroundPointerOver, Is.Not.Null, $"{className}: the theme names a hover fill");
        Assert.That(plate.Background, Is.SameAs(chip.BackgroundPointerOver),
            $"{className}: the pointer has to reach the plate, not just the brush the style declared");
    }

    [Test]
    public void ACaptionCommandLightsUpUnderThePointer()
    {
        Lights(new Button(), "CaptionCommand");
    }

    [Test]
    public void AQuickAccessCommandLightsUpUnderThePointer()
    {
        Lights(new Button(), "QuickAccessButton");
    }

    [Test]
    public void AQuickAccessToggleLightsUpUnderThePointer()
    {
        Lights(new ToggleButton(), "QuickAccessButton");
    }

    [TestCase("GalleryArrow")]
    [TestCase("RibbonScrollButton")]
    public void ARepeatingArrowLightsUpUnderThePointer(string className)
    {
        Lights(new RepeatButton(), className);
    }

    [Test]
    public void TheGalleryChevronLightsUpUnderThePointer()
    {
        Lights(new Button(), "GalleryArrow");
    }

    [Test]
    public void ACollapsedGroupChipLightsUpUnderThePointer()
    {
        Lights(new ToggleButton(), "CollapsedGroupButton");
    }
}
