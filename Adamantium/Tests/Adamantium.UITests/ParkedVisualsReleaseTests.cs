using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

[TestFixture]
public class ParkedVisualsReleaseTests
{
    private static readonly object Owner = new();

    [SetUp]
    [TearDown]
    public void ResetStore()
    {
        ParkedVisuals.Clear();
    }

    [Test]
    public void Release_DiscardsARequiredViewAndItsChildren()
    {
        var key = new object();
        var child = new Border();
        var view = new ParkedView { Child = child };
        ParkedVisuals.Keep(Owner, key, view);

        ParkedVisuals.ReleaseAbsent(Owner, _ => false);

        Assert.That(ParkedVisuals.TryTake(Owner, key, null, out _, out _, out _, out _), Is.False);
        Assert.That(view.IsDiscarded, Is.True);
        Assert.That(child.IsDiscarded, Is.True);
    }

    [Test]
    public void Release_KeepsWhatTheOwnerStillHolds()
    {
        var released = new object();
        var kept = new object();
        var keptView = new ParkedView();
        ParkedVisuals.Keep(Owner, released, new ParkedView());
        ParkedVisuals.Keep(Owner, kept, keptView);

        ParkedVisuals.ReleaseAbsent(Owner, key => key == kept);

        Assert.That(keptView.IsAwaitingReturn, Is.True);
        Assert.That(ParkedVisuals.TryTake(Owner, kept, null, out _, out _, out _, out _), Is.True);
        Assert.That(ParkedVisuals.TryTake(Owner, released, null, out _, out _, out _, out _), Is.False);
    }

    [Test]
    public void Release_LeavesAnotherOwnersViews()
    {
        var key = new object();
        var otherOwner = new object();
        ParkedVisuals.Keep(otherOwner, key, new ParkedView());

        ParkedVisuals.ReleaseAbsent(Owner, _ => false);

        Assert.That(ParkedVisuals.TryTake(otherOwner, key, null, out _, out _, out _, out _), Is.True);
    }

    [Test]
    public void Clear_DiscardsWhatWasKept()
    {
        var view = new ParkedView();
        ParkedVisuals.Keep(Owner, new object(), view);

        ParkedVisuals.Clear();

        Assert.That(view.IsDiscarded, Is.True);
    }

    private class ParkedView : Border
    {
        public override NavigationCacheMode KeepAlive => NavigationCacheMode.Required;
    }
}
