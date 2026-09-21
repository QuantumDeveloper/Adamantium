using System.Collections.ObjectModel;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A list that gains an item gets WIDER, and everything laid out beside it has to move over. The caption is
/// where this shows: a quick-access bar grows by a button and the window title must step aside for it, without waiting
/// for a resize to shake the layout loose.</summary>
[TestFixture]
public class ItemsGrowthInvalidationTests
{
    private static ItemsControl BarOf(ObservableCollection<string> source)
    {
        var bar = new ItemsControl
        {
            ItemsSource = source,
            ItemTemplate = new DataTemplate(() => new TemplateResult
            {
                RootComponent = new Border { Width = 30, Height = 20 }
            }),
            Template = new ControlTemplate(() =>
            {
                var presenter = new ItemsPresenter();
                var result = new TemplateResult { RootComponent = presenter };
                result.RegisterName("PART_ItemsPresenter", presenter);
                return result;
            })
        };

        bar.ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
        {
            RootComponent = new StackPanel { Orientation = Orientation.Horizontal }
        });

        return bar;
    }

    // The Auto column is as wide as the bar in it. Add a button and it has to be wider - that widening is what pushes
    // whatever shares the row along.
    [Test]
    public void AListThatGainsAnItem_WidensTheAutoColumnItSitsIn()
    {
        var source = new ObservableCollection<string> { "one", "two" };
        var bar = BarOf(source);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var title = new Border();
        Grid.SetColumn(title, 1);
        grid.Children.Add(bar);
        grid.Children.Add(title);

        // Through the real layout pass: propagating a size change UP is the layout manager's job, and calling Measure
        // by hand skips it - the grid is not invalid, so it would simply decline to measure again.
        var window = new Window { Width = 400, Height = 200, Content = grid };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);
        var before = title.Bounds.X;

        source.Add("three");

        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);

        Assert.That(title.Bounds.X, Is.GreaterThan(before),
            "the bar grew by a button and what sits beside it never moved over");
    }
}
