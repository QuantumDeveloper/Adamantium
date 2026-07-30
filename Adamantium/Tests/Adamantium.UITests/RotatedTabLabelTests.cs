using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

// Where a TURNED tab label actually lands inside its tab. The label of a tool panel folded against a side edge is the
// FluentDark TabItem chrome with a quarter-turned header template inside it, and it must sit in the middle of the tab
// both ways - it read as pushed against the tab's top edge on screen.
[TestFixture]
public class RotatedTabLabelTests
{
    private const double LabelWidth = 60;    // the "text" - a fixed box, so the numbers do not depend on font metrics
    private const double LabelHeight = 16;

    // Mirrors FluentDark's TabItem chrome: Border(Padding) -> StackPanel(Horizontal, centred) -> [icon, header, close].
    private static ControlTemplate TabChrome() => new(() =>
    {
        var border = new Border();
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        // Collapsed exactly as the theme leaves it with no icon - its 6px margin would otherwise be reserved anyway.
        var icon = new ContentPresenter
        {
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 6, 0)
        };
        var header = new ContentPresenter
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var close = new Button { Visibility = Visibility.Collapsed };

        row.Children.Add(icon);
        row.Children.Add(header);
        row.Children.Add(close);
        border.Child = row;

        var result = new TemplateResult { RootComponent = border };
        result.RegisterName("TabBorder", border);
        result.RegisterName("PART_Icon", icon);
        result.RegisterName("PART_ContentPresenter", header);
        result.RegisterName("PART_CloseButton", close);
        result.AddTemplateBinding(border, "Padding", new TemplateBinding { Path = "Padding" });
        result.AddTemplateBinding(header, "Content", new TemplateBinding { Path = "Header" });
        result.AddTemplateBinding(header, "ContentTemplate", new TemplateBinding { Path = "HeaderTemplate" });
        return result;
    });

    // The header template the Pane[LabelRotation=Left] style installs: a presenter turned a quarter turn.
    private static DataTemplate TurnedLabel() => new(() => new TemplateResult
    {
        RootComponent = new ContentPresenter
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LayoutTransform = new Transform { RotationAngle = -90 },
            Content = new Border { Width = LabelWidth, Height = LabelHeight }
        }
    });

    private static (TabItem tab, ContentPresenter header) TurnedTab(Thickness padding)
    {
        var tab = new TabItem
        {
            Padding = padding,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Header = new object(),
            HeaderTemplate = TurnedLabel(),
            Template = TabChrome()
        };

        var strip = new TabPanel { Orientation = Orientation.Vertical };
        strip.Children.Add(tab);
        strip.Measure(new Size(400, 400));
        strip.Arrange(new Rect(0, 0, 400, 400));

        var header = (ContentPresenter)tab.GetTemplateChild("PART_ContentPresenter");
        return (tab, header);
    }

    /// <summary>Where a part sits in the TAB's own space - Bounds are parent-relative, and the label is two levels down
    /// (border padding, then the centred row), so the offsets have to be summed to compare with the tab at all.</summary>
    private static Rect InTabSpace(TabItem tab, IUIComponent part)
    {
        var x = 0.0;
        var y = 0.0;
        for (var c = part; c != null && !ReferenceEquals(c, tab); c = c.VisualParent as IUIComponent)
        {
            x += c.Bounds.X;
            y += c.Bounds.Y;
        }
        return new Rect(new Vector2((float)x, (float)y), part.Bounds.Size);
    }

    // With a SYMMETRIC padding the turned label must sit dead centre in the tab, both ways.
    [Test]
    public void TurnedLabel_SitsInTheMiddleOfItsTab()
    {
        var (tab, header) = TurnedTab(new Thickness(6));
        var label = InTabSpace(tab, header);

        Assert.Multiple(() =>
        {
            Assert.That(label.Y + label.Height / 2, Is.EqualTo(tab.Bounds.Height / 2).Within(0.5),
                $"centred DOWN the tab (tab {tab.Bounds.Size}, label {label})");
            Assert.That(label.X + label.Width / 2, Is.EqualTo(tab.Bounds.Width / 2).Within(0.5),
                $"centred ACROSS the tab (tab {tab.Bounds.Size}, label {label})");
        });
    }

    // The padding FluentDark gives a tab: wider than tall, and stated on all four sides. Written as the two-value
    // `12 6` it meant Thickness(leftTop, rightBottom) - 12 on the left AND top against 6 on the right and bottom - which
    // put a turned label 3px off centre each way.
    [Test]
    public void TurnedLabel_WithTheThemesPadding_IsStillCentred()
    {
        var (tab, header) = TurnedTab(new Thickness(12, 6, 12, 6));
        var label = InTabSpace(tab, header);

        Assert.Multiple(() =>
        {
            Assert.That(label.Y + label.Height / 2, Is.EqualTo(tab.Bounds.Height / 2).Within(0.5),
                $"centred DOWN the tab (tab {tab.Bounds.Size}, label {label})");
            Assert.That(label.X + label.Width / 2, Is.EqualTo(tab.Bounds.Width / 2).Within(0.5),
                $"centred ACROSS the tab (tab {tab.Bounds.Size}, label {label})");
        });
    }

    // The turned footprint: as WIDE as the label is tall, as TALL as the label is long - plus the padding, and nothing
    // else. A tab with no icon must reserve nothing for one, or the column is 6px wider than it needs to be.
    [Test]
    public void TurnedLabel_TurnsTheTabsFootprint()
    {
        var (tab, _) = TurnedTab(new Thickness(6));

        Assert.Multiple(() =>
        {
            Assert.That(tab.Bounds.Width, Is.EqualTo(LabelHeight + 12).Within(0.5), "a narrow column - no dead space where the icon is not");
            Assert.That(tab.Bounds.Height, Is.EqualTo(LabelWidth + 12).Within(0.5), "as tall as the label is long");
        });
    }
}
