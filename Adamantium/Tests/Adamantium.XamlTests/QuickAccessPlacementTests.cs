using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// The quick-access bar is placed by having TWO instances of it - one in the caption, one under the ribbon - each
/// showing itself only while the placement names its own slot. Which slot an instance stands in is read off the tree
/// (a caption is a TitleBar, anything else is the ribbon's own row), so the whole arrangement rests on that reading
/// being right at the moment it is taken.
/// </summary>
[TestFixture]
public class QuickAccessPlacementTests
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
        var theme = new Adamantium.UI.Themes.FluentTheme.Fluent();
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);
    }

    // The slot reading itself is right - measured in the running shell. What was left is ROOM: the footer sits inside
    // the ribbon's clipping root, so a ribbon whose height does not grow to include it shows nothing there however
    // visible the bar is.
    [Test]
    public void TheFooterGetsRoomUnderTheBand()
    {
        var bar = new Adamantium.UI.Controls.Decorators.Border { Height = 26, Width = 200 };
        var ribbon = new Ribbon { FooterContent = bar };

        ribbon.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(ribbon);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        ribbon.Measure(new Size(900, double.PositiveInfinity));
        ribbon.Arrange(new Rect(0, 0, 900, ribbon.DesiredSize.Height));

        TestContext.WriteLine($"ribbon desired={ribbon.DesiredSize} groupsArea={ribbon.GroupsAreaHeight}");
        TestContext.WriteLine($"footer render={bar.RenderSize} desired={bar.DesiredSize}");

        Assert.That(bar.RenderSize.Height, Is.EqualTo(26).Within(0.5),
            "the footer row is Auto - whatever the application puts under the band must be arranged at its own height");
        Assert.That(ribbon.DesiredSize.Height, Is.GreaterThan(ribbon.GroupsAreaHeight + 20),
            "and the ribbon must ASK for that room, or its own clip cuts the row off");
    }

    // The two bars are two ItemsControls over ONE collection. Each has to build its own containers from that data - if
    // the items were containers rather than data, the first host would own them and the second would come up empty.
    [Test]
    public void ABarUnderTheRibbonBuildsItsOwnItems()
    {
        var items = new System.Collections.ObjectModel.ObservableCollection<object> { "One", "Two", "Three" };

        var bar = new RibbonQuickAccess
        {
            Placement = RibbonQuickAccessPlacement.BelowRibbon,
            ItemsSource = items,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var ribbon = new Ribbon { FooterContent = bar };

        ribbon.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(ribbon);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        ribbon.Measure(new Size(900, double.PositiveInfinity));
        ribbon.Arrange(new Rect(0, 0, 900, ribbon.DesiredSize.Height));

        var host = bar.ItemsHostPanel;
        TestContext.WriteLine($"bar visibility={bar.Visibility} render={bar.RenderSize} desired={bar.DesiredSize}");
        TestContext.WriteLine($"host={host?.GetType().Name} children={host?.Children.Count} " +
                              $"hostRender={(host as UIComponent)?.RenderSize}");
        var overflow = 0;
        if (bar.OverflowItems != null) foreach (var _ in bar.OverflowItems) overflow++;
        TestContext.WriteLine($"overflow items={overflow}");
        TestContext.WriteLine($"bar foreground={bar.Foreground?.ToString() ?? "<null>"}");

        Assert.Multiple(() =>
        {
            Assert.That(bar.Visibility, Is.EqualTo(Visibility.Visible), "the placement names this slot");
            Assert.That(host?.Children.Count, Is.EqualTo(3), "each bar generates its own containers from the data");
            Assert.That(bar.RenderSize.Width, Is.GreaterThan(0), "and it is given room to draw them in");

            // The one that was actually wrong, and the one nothing else would have caught: every command in the bar
            // strokes itself with the BAR's foreground, so a transparent one is a row of buttons drawn in nothing.
            // The caption's instance is handed the title bar's colour by the shell; this one has no title bar to ask.
            Assert.That(((SolidColorBrush)bar.Foreground)?.Color.A, Is.GreaterThan(0),
                "a bar nobody dressed still has to draw - its icons take their stroke from this");
        });
    }

    // A theme swap re-styles and re-templates everything in place. The bar has to come out of it still drawing: its
    // containers are rebuilt, and every icon in them takes its stroke from the bar through an {Ancestor} binding -
    // which resolves against the TREE, and a container built while its subtree is detached has no tree to resolve in.
    [Test]
    public void TheBarStillDrawsAfterAThemeSwap()
    {
        var items = new System.Collections.ObjectModel.ObservableCollection<object> { "One", "Two", "Three" };
        var bar = new RibbonQuickAccess
        {
            Placement = RibbonQuickAccessPlacement.BelowRibbon,
            ItemsSource = items,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var ribbon = new Ribbon { FooterContent = bar };

        ribbon.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(ribbon);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        ribbon.Measure(new Size(900, double.PositiveInfinity));
        ribbon.Arrange(new Rect(0, 0, 900, ribbon.DesiredSize.Height));

        var before = (SolidColorBrush)bar.Foreground;
        var childrenBefore = bar.ItemsHostPanel?.Children.Count;
        TestContext.WriteLine($"before swap: foreground={before} children={childrenBefore}");

        var macOs = new Adamantium.UI.Themes.MacOsTheme.MacOs();
        _app.ThemeManager.AddTheme(macOs.Name, macOs);
        _app.ThemeManager.SetTheme(macOs);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        ribbon.Measure(new Size(900, double.PositiveInfinity));
        ribbon.Arrange(new Rect(0, 0, 900, ribbon.DesiredSize.Height));

        var after = (SolidColorBrush)bar.Foreground;
        var childrenAfter = bar.ItemsHostPanel?.Children.Count;
        TestContext.WriteLine($"after swap:  foreground={after} children={childrenAfter} " +
                              $"render={bar.RenderSize}");

        Assert.Multiple(() =>
        {
            Assert.That(childrenAfter, Is.EqualTo(3), "the bar keeps its commands across a swap");
            Assert.That(after?.Color.A, Is.GreaterThan(0), "and still has ink to draw them with");
            Assert.That(bar.RenderSize.Width, Is.GreaterThan(0), "and room to draw them in");
        });
    }

    // The bar's own brush survives a swap; what the user loses is the ink INSIDE the containers. A presenter STAMPS
    // its Foreground onto the content it builds, so the question is whether the content built for the new theme is
    // stamped at all - measured on a gallery cell, where the content is a label and nothing else.
    [Test]
    public void ContentInsideACellKeepsItsInkAcrossAThemeSwap()
    {
        var label = new Adamantium.UI.Controls.Text.TextBlock { Text = "Steel", FontSize = 10 };
        var cell = new RibbonGalleryItem { Content = label };

        cell.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(cell);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        cell.Measure(new Size(200, 200));
        cell.Arrange(new Rect(0, 0, 200, 200));

        var presenter = cell.GetTemplateChild("PART_ContentPresenter") as UIComponent;
        TestContext.WriteLine($"before: cell={cell.Foreground} presenter={presenter?.Foreground} label={label.Foreground}");

        var macOs = new Adamantium.UI.Themes.MacOsTheme.MacOs();
        _app.ThemeManager.AddTheme(macOs.Name, macOs);
        _app.ThemeManager.SetTheme(macOs);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        cell.Measure(new Size(200, 200));
        cell.Arrange(new Rect(0, 0, 200, 200));

        presenter = cell.GetTemplateChild("PART_ContentPresenter") as UIComponent;
        TestContext.WriteLine($"after:  cell={cell.Foreground} presenter={presenter?.Foreground} label={label.Foreground}");

        Assert.That(((SolidColorBrush)label.Foreground)?.Color.A, Is.GreaterThan(0),
            "the label has to come out of the swap with ink - the cell kept its own");
    }
}
