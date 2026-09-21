using System.Linq;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Themes.MacOsTheme;
using NUnit.Framework;

namespace Adamantium.XamlTests;

[TestFixture]
public class MacOsSliderKnobReachTests
{
    private const double Width = 200;
    private const double Height = 28;

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

    [TestCase(0, false)]
    [TestCase(100, false)]
    [TestCase(0, true)]
    [TestCase(100, true)]
    public void WhereEverythingSitsAt(double value, bool snap)
    {
        var slider = new Slider
        {
            Minimum = 0, Maximum = 100, Value = value, Width = Width, Height = Height,
            TickFrequency = 1, IsSnapToTickEnabled = snap
        };
        slider.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(slider);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        slider.Measure(new Size(Width, Height));
        slider.Arrange(new Rect(0, 0, Width, Height));

        var rail = slider.GetTemplateChild("Rail") as IUIComponent;
        var fill = slider.GetTemplateChild("PART_SelectionRange") as IUIComponent;
        var knob = FindKnob(slider);

        Assert.That(rail, Is.Not.Null, "the template has a Rail");
        Assert.That(knob, Is.Not.Null, "the template has a knob");

        TestContext.WriteLine($"value={value}, snap={snap}, actual Value={slider.Value}, control width={Width}");
        TestContext.WriteLine($"  rail  x {AbsoluteLeft(rail, slider)} .. {AbsoluteRight(rail, slider)}");
        TestContext.WriteLine($"  knob  x {AbsoluteLeft(knob, slider)} .. {AbsoluteRight(knob, slider)}");
        if (fill != null)
            TestContext.WriteLine($"  fill  x {AbsoluteLeft(fill, slider)} .. {AbsoluteRight(fill, slider)} (width {fill.Bounds.Width})");

        Assert.Pass("reporting only");
    }

    private static double AbsoluteLeft(IUIComponent node, IUIComponent root)
    {
        var x = 0.0;
        for (var n = node; n != null && n != root; n = n.VisualParent as IUIComponent) x += n.Bounds.X;
        return x;
    }

    private static double AbsoluteRight(IUIComponent node, IUIComponent root)
    {
        var x = 0.0;
        for (var n = node; n != null && n != root; n = n.VisualParent as IUIComponent) x += n.Bounds.X;
        return x + node.Bounds.Width;
    }

    private static IUIComponent FindKnob(IUIComponent node)
    {
        foreach (var child in node.VisualChildren.OfType<IUIComponent>())
        {
            if (child is Thumb) return child;
            if (FindKnob(child) is { } found) return found;
        }

        return null;
    }
}
