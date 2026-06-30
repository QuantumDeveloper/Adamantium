using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Text;
using NUnit.Framework;

namespace Adamantium.UITests;

// The recycling fast-path that keeps a virtualized list from churning GPU buffers on every scroll frame: rebinding a
// ContentPresenter to new content of the SAME shape reuses the existing visual instead of tearing it down and rebuilding
// it (which exhausted device memory under fast scroll for plain-string items).
public class ContentPresenterTests
{
    [Test]
    public void StringRebind_ReusesAutoTextBlock()
    {
        var presenter = new ContentPresenter { Content = "a" };
        presenter.Measure(new Size(120, 30));
        var first = presenter.VisualChildren.OfType<TextBlock>().FirstOrDefault();
        Assert.That(first, Is.Not.Null, "string content is hosted in an auto-generated TextBlock");

        presenter.Content = "b";                 // a recycled container rebinding to another string item
        presenter.Measure(new Size(120, 30), force: true);
        var second = presenter.VisualChildren.OfType<TextBlock>().FirstOrDefault();

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.SameAs(first), "the TextBlock is reused (not recreated -> no per-rebind GPU buffer churn)");
            Assert.That(second.Text, Is.EqualTo("b"), "and its text is updated to the new item");
        });
    }
}
