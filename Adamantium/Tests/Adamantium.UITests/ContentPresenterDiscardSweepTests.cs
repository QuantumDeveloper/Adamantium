using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// When a subtree is discarded, every presenter still naming one of its roots drops its handles on it. Dropping a
/// HANDLE is not the same as letting the child GO, and the two were confused: the sweep assumed a discarded root has no
/// visual parent left, so the field was the only thing still naming it. That holds when the PRESENTER is the one being
/// destroyed and fails the other way round - a root discarded while its presenter goes on living is still one of that
/// presenter's children.
/// <para>Forgotten but not removed, it stayed in the tree: laid out, drawn, and now untracked, so no later content swap
/// could ever release it. Measured on docking - a pane's authored body stayed under the view that replaced it and the
/// two drew on top of each other for the rest of the session.</para>
/// </summary>
[TestFixture]
public class ContentPresenterDiscardSweepTests
{
    private static int VisualChildCount(IUIComponent c)
    {
        var n = 0;
        foreach (var _ in c.VisualChildren) n++;
        return n;
    }

    [Test]
    public void AContentDiscardedUnderALivingPresenter_LeavesNoChildBehind()
    {
        var presenter = new ContentPresenter();
        var body = new StackPanel();
        presenter.Content = body;
        presenter.Measure(new Size(100, 100));

        Assert.That(VisualChildCount(presenter), Is.EqualTo(1), "the presenter is showing it to begin with");

        // The CONTENT is destroyed while the presenter lives on - a pane closing, a template torn down under a host
        // that was not. Marked, then DRAINED: the sweep runs off the drain, not off the mark (teardown is paid for in
        // the loop's idle time, which is the whole reason the queue exists).
        DiscardedVisuals.Publish(body);
        while (DiscardedVisuals.Drain(64) > 0) { }

        Assert.That(VisualChildCount(presenter), Is.EqualTo(0),
            "a handle dropped without letting the child go leaves it in the tree, untracked and still drawn");
    }
}
