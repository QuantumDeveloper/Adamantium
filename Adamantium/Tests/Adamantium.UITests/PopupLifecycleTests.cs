using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// A popup's content is drawn by the window's overlay but is nobody's visual child, so the ordinary attach - which walks
// down visual parents - never reached it. It therefore never learned which root it lived in, and the two things
// attaching does passed popups by: re-recording what was freed while it was away, and restarting suspended triggers.
[TestFixture]
public class PopupLifecycleTests
{
    private class Content : Border
    {
        public int Attached;
        public int Detached;

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Attached++;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            Detached++;
        }
    }

    private static (Popup popup, Content content, Window window) Shown()
    {
        var content = new Content { Width = 80, Height = 40 };
        var anchor = new Border { Width = 20, Height = 20 };
        var popup = new Popup { Child = content, PlacementTarget = anchor };

        var host = new StackPanel();
        host.Children.Add(anchor);
        host.Children.Add(popup);

        var window = new Window { Width = 400, Height = 300, Content = host };
        Settle(window);

        return (popup, content, window);
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);
    }

    [Test]
    public void OpeningAttachesTheContentToTheHostWindow()
    {
        var (popup, content, window) = Shown();
        popup.IsOpen = true;
        Settle(window);

        Assert.Multiple(() =>
        {
            Assert.That(content.IsAttachedToVisualTree, Is.True, "the content must know which root draws it");
            Assert.That(content.RootVisual, Is.SameAs(window));
            Assert.That(content.Attached, Is.EqualTo(1));
        });
    }

    [Test]
    public void ClosingDetachesIt()
    {
        var (popup, content, window) = Shown();
        popup.IsOpen = true;
        Settle(window);
        popup.IsOpen = false;
        Settle(window);

        Assert.Multiple(() =>
        {
            Assert.That(content.IsAttachedToVisualTree, Is.False);
            Assert.That(content.Detached, Is.EqualTo(1));
        });
    }

    [Test]
    public void EveryShowingIsAnAttachOfItsOwn()
    {
        var (popup, content, window) = Shown();

        for (var i = 0; i < 3; i++)
        {
            popup.IsOpen = true;
            Settle(window);
            popup.IsOpen = false;
            Settle(window);
        }

        Assert.Multiple(() =>
        {
            Assert.That(content.Attached, Is.EqualTo(3), "a re-shown popup must be re-recorded, not left geometry-valid");
            Assert.That(content.Detached, Is.EqualTo(3));
        });
    }
}
