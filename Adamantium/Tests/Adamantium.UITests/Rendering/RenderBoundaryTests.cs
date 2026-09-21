using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// One component that throws out of OnRender must cost ONE component, not the frame. The record walk visits the whole
/// scene in a single pass, so a throw abandoned it: nothing after the bad component was recorded and nothing before it
/// was published either, and the window came up as a bare frame. It also repeated at frame rate - the failure left the
/// component dirty, so the next frame tried again, forever.
/// </summary>
[TestFixture]
public class RenderBoundaryTests
{
    private sealed class Exploding : Border
    {
        public int Attempts;

        protected override void OnRender(IDrawingContext context)
        {
            Attempts++;
            throw new InvalidOperationException("a brush of the wrong type, as it happens");
        }
    }

    [Test]
    public void AThrowingComponentDoesNotTakeTheFrameWithIt()
    {
        var bad = new Exploding();
        var good = new Border { Background = Adamantium.UI.Core.Media.Brushes.Red };

        Assert.DoesNotThrow(() => bad.Render(new DrawingContext()),
            "the boundary keeps the walk going; the component simply draws nothing");
        Assert.DoesNotThrow(() => good.Render(new DrawingContext()),
            "and everything after it still records");
    }

    [Test]
    public void ItIsNotRetriedEveryFrame()
    {
        var bad = new Exploding();

        for (var frame = 0; frame < 10; frame++) bad.Render(new DrawingContext());

        TestContext.WriteLine($"OnRender attempts over 10 frames: {bad.Attempts}");
        Assert.That(bad.Attempts, Is.EqualTo(1),
            "a failure that leaves the component dirty is a failure repeated at frame rate - 15MB of identical stacks " +
            "in under a minute is what that cost");
    }

    // ...and a fix at runtime still takes effect: whatever next invalidates the component gets a fresh attempt.
    [Test]
    public void InvalidatingItAsksAgain()
    {
        var bad = new Exploding();
        bad.Render(new DrawingContext());
        bad.InvalidateRender(false);
        bad.Render(new DrawingContext());

        Assert.That(bad.Attempts, Is.EqualTo(2));
    }
}
