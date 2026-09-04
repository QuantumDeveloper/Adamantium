using System.Linq;
using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Themes.FluentTheme;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>Where the gap between tiles on the Layout tab actually comes from, in pixels, under a real theme.
/// <para>Reported from the stand as "giant margins between containers", and every guess so far has been wrong: the
/// panel hands out exact cells, and the row's Padding really is 0 (ItemContainerStyleVsThemeTests). So this walks the
/// chain instead of arguing about it - the cell, then the container's rect, then the tile's - and prints what each
/// step costs.</para>
/// </summary>
[TestFixture]
public class TileInsetChainTests
{
    private const double Cell = 120;
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
    }

    [TestCase("Fluent")]
    [TestCase("macOS")]
    public void WhatEatsTheCell(string which)
    {
        var themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = themes;
        ITheme theme = which == "macOS"
            ? new Adamantium.UI.Themes.MacOsTheme.MacOs()
            : new Fluent();
        // Without this the theme is set but nothing is TEMPLATED - the containers come out as bare TextBlocks and the
        // whole measurement reads "nothing insets anything", which is what the first two versions of this test said.
        ((FakeContext)_app.UIContext).ThemeEngine = themes;
        themes.AddTheme(theme.Name, theme);
        themes.SetTheme(theme);
        TestContext.WriteLine($"--- theme: {theme.Name} ---");

        // The Layout tab's own container style: no row chrome at all, content stretched.
        var containerStyle = new Style();
        containerStyle.Selector.Types.Add(typeof(ListBoxItem));
        containerStyle.Setters.Add(new Setter(nameof(ListBoxItem.Padding), new Thickness(0)));
        containerStyle.Setters.Add(new Setter(nameof(ListBoxItem.HorizontalContentAlignment), HorizontalAlignment.Stretch));
        containerStyle.Setters.Add(new Setter(nameof(ListBoxItem.VerticalContentAlignment), VerticalAlignment.Stretch));

        // The list needs a template that exposes PART_ItemsPresenter, or nothing is generated and the whole measurement
        // reads "no containers" - which is what the first version of this test reported.
        var list = new ListBox
        {
            Template = new ControlTemplate(() =>
            {
                var presenter = new ItemsPresenter();
                var result = new TemplateResult { RootComponent = presenter };
                result.RegisterName("PART_ItemsPresenter", presenter);
                return result;
            }),
            ItemsSource = Enumerable.Range(0, 200).Cast<object>().ToList(),
            ItemContainerStyle = containerStyle,
            // The tab's tile: a stretched rounded rectangle inset by 3.
            ItemTemplate = new DataTemplate(() => new TemplateResult
            {
                RootComponent = new Adamantium.UI.Controls.Shapes.Rectangle
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Margin = new Thickness(3)
                }
            }),
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
            {
                RootComponent = new WrapPanel
                {
                    Orientation = Orientation.Horizontal,
                    ItemWidth = Cell,
                    ItemHeight = Cell,
                    ScrollBindBudget = 0
                }
            })
        };

        list.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(list);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        list.Measure(new Size(800, 600));
        list.Arrange(new Rect(0, 0, 800, 600));

        var row = (IUIComponent)list.ItemContainerGenerator.ContainerFromIndex(0);
        Assert.That(row, Is.Not.Null, "a container was realized");

        var container = (ListBoxItem)row;
        TestContext.WriteLine($"cell asked for            = {Cell} x {Cell}");
        TestContext.WriteLine($"container rect            = {row.Bounds}");
        TestContext.WriteLine($"container Margin          = {container.Margin}");
        TestContext.WriteLine($"container Padding         = {container.Padding}");
        TestContext.WriteLine($"container MinWidth/Height = {container.MinWidth} / {container.MinHeight}");

        var second = (IUIComponent)list.ItemContainerGenerator.ContainerFromIndex(1);
        if (second != null)
            TestContext.WriteLine($"gap between container 0 and 1 = {second.Bounds.X - (row.Bounds.X + row.Bounds.Width)}");

        // ...and everything BELOW the container, which is where the inset has to be if the container itself is clean.
        Walk(row, 0);

        var tile = FindTile(row);
        Assert.That(tile, Is.Not.Null, "the item template produced a tile");

        Assert.Multiple(() =>
        {
            Assert.That(container.Padding, Is.EqualTo(new Thickness(0)),
                "the VIEW asked its rows for no padding; a theme's rule for the type must not overrule it");

            // The tile keeps the cell less its OWN margin and nothing else. The chrome margin a theme puts on the row
            // (macOS insets its plate by 4,1) is the theme's business and stays; the row's text padding is not.
            var ownMargin = 6;                      // Margin="3" on each side
            var chrome = which == "macOS" ? 8 : 0;  // the plate's inset, horizontal
            Assert.That(tile.Bounds.Width, Is.EqualTo(Cell - ownMargin - chrome).Within(0.5));
        });
    }

    private static IUIComponent FindTile(IUIComponent node)
    {
        foreach (var child in node.VisualChildren.OfType<IUIComponent>())
        {
            if (child is Adamantium.UI.Controls.Shapes.Rectangle) return child;
            if (FindTile(child) is { } found) return found;
        }

        return null;
    }

    private static void Walk(IUIComponent node, int depth)
    {
        foreach (var child in node.VisualChildren.OfType<IUIComponent>())
        {
            var pad = child is Control c ? $", Padding={c.Padding}" : "";
            TestContext.WriteLine($"{new string(' ', (depth + 1) * 2)}{child.GetType().Name}: " +
                                  $"bounds={child.Bounds}, Margin={((MeasurableUIComponent)child).Margin}{pad}");
            Walk(child, depth + 1);
        }

        Assert.Pass("reporting only");
    }
}
