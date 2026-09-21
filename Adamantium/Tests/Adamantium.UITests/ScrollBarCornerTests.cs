using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Controls;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The square where two scrollbars cross belongs to neither of them, and a theme cannot see that for itself:
/// each bar's visibility is decided from the metrics inside the viewer, and a style trigger cannot ask a sibling part
/// how it came out.
/// <para>Why it matters: the shipped templates shortened the horizontal bar by a bar's width UNCONDITIONALLY, so a
/// viewer with only a horizontal bar left a gap at the right for a vertical bar that was not there.</para>
/// </summary>
[TestFixture]
public class ScrollBarCornerTests
{
    /// <summary>The template the real ones are: a presenter with both bars over it, by the names the control looks
    /// up.</summary>
    private static ControlTemplate BothBars() => new(() =>
    {
        var presenter = new ScrollContentPresenter();
        var vertical = new ScrollBar { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Right };
        var horizontal = new ScrollBar { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };

        var grid = new Adamantium.UI.Controls.Panels.Grid();
        ((IContainer)grid).AddOrSetChildComponent(presenter);
        ((IContainer)grid).AddOrSetChildComponent(vertical);
        ((IContainer)grid).AddOrSetChildComponent(horizontal);

        var result = new TemplateResult();
        result.RegisterName("PART_ScrollContentPresenter", presenter);
        result.RegisterName("PART_VerticalScrollBar", vertical);
        result.RegisterName("PART_HorizontalScrollBar", horizontal);
        result.RootComponent = grid;
        return result;
    });

    private static ScrollViewer Viewer(double contentWidth, double contentHeight)
    {
        var viewer = new ScrollViewer
        {
            Width = 200,
            Height = 200,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Template = BothBars(),
            Content = new Border { Width = contentWidth, Height = contentHeight }
        };

        // The shipped template binds the presenter's Content to the viewer's; this one is built by hand, so it has to be
        // handed over here. Without it the presenter measures nothing, no bar is ever shown, and every case in this
        // fixture would agree on "the corner is free" for the wrong reason.
        ((ScrollContentPresenter)viewer.GetTemplateChild("PART_ScrollContentPresenter")).Content = viewer.Content;

        // TWO passes: the first measures the content and produces the metrics, the second is the one that can decide a
        // bar's visibility from them.
        var root = new Border { Width = 400, Height = 400, Child = viewer };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);

        var vertical = (IUIComponent)viewer.GetTemplateChild("PART_VerticalScrollBar");
        var horizontal = (IUIComponent)viewer.GetTemplateChild("PART_HorizontalScrollBar");
        TestContext.WriteLine($"content {contentWidth}x{contentHeight}: V={vertical.Visibility} " +
                              $"H={horizontal.Visibility} corner={viewer.IsScrollBarCornerOccupied}");
        return viewer;
    }

    [Test]
    public void NeitherAxisOverflows_TheCornerIsFree()
    {
        Assert.That(Viewer(100, 100).IsScrollBarCornerOccupied, Is.False);
    }

    [Test]
    public void OnlyTheVerticalOverflows_TheCornerIsFree()
    {
        Assert.That(Viewer(100, 900).IsScrollBarCornerOccupied, Is.False,
            "one bar owns its whole edge - nothing is crossing it");
    }

    [Test]
    public void OnlyTheHorizontalOverflows_TheCornerIsFree()
    {
        Assert.That(Viewer(900, 100).IsScrollBarCornerOccupied, Is.False,
            "this is the case the unconditional inset got wrong");
    }

    [Test]
    public void BothOverflow_TheCornerIsOccupied()
    {
        Assert.That(Viewer(900, 900).IsScrollBarCornerOccupied, Is.True);
    }
}
