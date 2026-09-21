using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// The inspector INSIDE a page's layout - the shape the demo puts it in: a Grid with a star row, two columns, captions
/// above and a row of controls below. Built here because the page came up empty and nothing was logged, which is a
/// layout answer rather than a failure, and layout answers have to be measured.
/// </summary>
[TestFixture]
public class PropertyGridInLayoutTests
{
    private FakeApp _app;
    private ThemeManager _themes;

    private sealed class Target
    {
        public string Name { get; set; } = "entity";
        public double Scale { get; set; } = 1.5;
    }

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

        var theme = new Adamantium.UI.Themes.MacOsTheme.MacOs();
        _themes.AddTheme(theme.Name, theme);
        _themes.SetTheme(theme);
    }

    private static PropertyGrid Inspector()
    {
        var section = new PropertySection { Header = "General", Target = new Target(), IsExpanded = true };
        section.Properties.Add(new StringProperty
        {
            Header = "Name", Binding = new Adamantium.UI.Core.Data.Binding("Name")
        });
        section.Properties.Add(new NumericProperty
        {
            Header = "Scale", Binding = new Adamantium.UI.Core.Data.Binding("Scale")
        });

        var grid = new PropertyGrid();
        grid.Sections.Add(section);
        return grid;
    }

    private static void Lay(IUIComponent root, Size size)
    {
        ((Adamantium.UI.Core.FundamentalUIComponent)root).ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        ((IMeasurableComponent)root).Measure(size);
        ((IMeasurableComponent)root).Arrange(new Rect(0, 0, size.Width, size.Height));
    }

    // The page's own shape, before any inspector is involved: five rows, two columns, some children spanning both.
    // The demo page came up with everything below its second row invisible, and a Grid is the first thing to ask.
    [Test]
    public void AFiveRowTwoColumnPageGivesEveryRowItsPlace()
    {
        var page = new Grid
        {
            RowDefinitions = Adamantium.Core.TypeParsing.TypeParser.Parse<RowDefinitions>("Auto,Auto,Auto,*,Auto"),
            ColumnDefinitions = Adamantium.Core.TypeParsing.TypeParser.Parse<ColumnDefinitions>("*,*")
        };

        var title = new TextBlock { Text = "title" };
        var description = new TextBlock { Text = "description" };
        var leftCaption = new TextBlock { Text = "left caption" };
        var rightCaption = new TextBlock { Text = "right caption" };
        var body = new Adamantium.UI.Controls.Decorators.Border();
        var footer = new TextBlock { Text = "footer" };

        Grid.SetRow(title, 0);
        Grid.SetColumnSpan(title, 2);
        Grid.SetRow(description, 1);
        Grid.SetColumnSpan(description, 2);
        Grid.SetRow(leftCaption, 2);
        Grid.SetColumn(leftCaption, 0);
        Grid.SetRow(rightCaption, 2);
        Grid.SetColumn(rightCaption, 1);
        Grid.SetRow(body, 3);
        Grid.SetColumnSpan(body, 2);
        Grid.SetRow(footer, 4);
        Grid.SetColumnSpan(footer, 2);

        foreach (var child in new IMeasurableComponent[] { title, description, leftCaption, rightCaption, body, footer })
            page.Children.Add(child);

        Lay(page, new Size(600, 400));

        TestContext.WriteLine($"rows={page.RowDefinitions.Count} title={title.Bounds} desc={description.Bounds} " +
                              $"left={leftCaption.Bounds} right={rightCaption.Bounds} body={body.Bounds} footer={footer.Bounds}");

        Assert.Multiple(() =>
        {
            Assert.That(page.RowDefinitions.Count, Is.EqualTo(5), "five rows were asked for");
            Assert.That(leftCaption.RenderSize.Height, Is.GreaterThan(0), "the third row's caption has a size");
            Assert.That(rightCaption.Bounds.X, Is.GreaterThan(200), "and the second column really is the second");
            Assert.That(footer.Bounds.Y, Is.GreaterThan(body.Bounds.Y), "the footer sits under the star row");
        });
    }

    // The plain case first, so the next test's failure means something: an inspector alone in a star row.
    [Test]
    public void AnInspectorInAStarRowGetsTheRow()
    {
        var page = new Grid();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var caption = new TextBlock { Text = "caption" };
        var inspector = Inspector();
        Grid.SetRow(caption, 0);
        Grid.SetRow(inspector, 1);
        page.Children.Add(caption);
        page.Children.Add(inspector);

        Lay(page, new Size(600, 400));

        TestContext.WriteLine($"caption={caption.RenderSize} inspector={inspector.RenderSize}");
        Assert.That(inspector.RenderSize.Height, Is.GreaterThan(50), "the inspector filled the star row");
    }

    // ...and now WRAPPED IN A STACK PANEL, which is what the demo page did when it came up blank. A vertical stack
    // measures its children with unbounded height, and an inspector is a scroller: asked how tall it would like to be
    // it can only answer "as tall as everything in me", and a star row is not obliged to give it that.
    [Test]
    public void AnInspectorInsideAStackPanelStillTakesRoom()
    {
        var page = new Grid();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var column = new StackPanel { Orientation = Orientation.Vertical };
        var caption = new TextBlock { Text = "caption" };
        var inspector = Inspector();
        column.Children.Add(caption);
        column.Children.Add(inspector);

        Grid.SetRow(column, 1);
        page.Children.Add(column);

        Lay(page, new Size(600, 400));

        TestContext.WriteLine($"stack={column.RenderSize} inspector={inspector.RenderSize} " +
                              $"desired={inspector.DesiredSize}");

        Assert.Multiple(() =>
        {
            Assert.That(column.RenderSize.Height, Is.GreaterThan(50), "the stack took the row");
            Assert.That(inspector.RenderSize.Height, Is.GreaterThan(50), "and the inspector inside it took room");
        });
    }
}
