using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

// Correctness contract for the recycling-list rebind optimisation, held for BOTH the recycling case (a virtualized list
// rebinding a container to another same-template item) and the GENERAL ContentPresenter case (a Button/header whose
// content changes to a different-sized element). Both must stay green.
[TestFixture]
public class ContentPresenterRebindTests
{
    // Invariant #2 (rebind = data-only): rebinding to a DIFFERENT item of the SAME DataTemplate must NOT re-measure the
    // reused template subtree. Without it, every scrolled row re-measured its whole tile subtree (the Layout-tab cost).
    [Test]
    public void DataRebind_SameTemplate_DoesNotReMeasureSubtree()
    {
        var template = new DataTemplate(() => new TemplateResult { RootComponent = new Border { Child = new Border() } });
        var cp = new ContentPresenter { ContentTemplate = template, Content = new object() };
        cp.Measure(new Size(24, 24));
        cp.Arrange(new Rect(0, 0, 24, 24));

        var m0 = MeasurableUIComponent.TotalMeasureCalls;
        // Exactly what ItemsControl.PrepareContainer does on a recycled rebind (same template):
        cp.DataContext = new object();
        cp.Content = new object();
        cp.Measure(new Size(24, 24));   // the panel re-measures the container at the (unchanged) cell

        var measures = MeasurableUIComponent.TotalMeasureCalls - m0;
        Assert.That(measures, Is.LessThanOrEqualTo(1),
            $"a same-template data rebind must not re-measure the reused subtree (got {measures} measure calls)");
    }

    // General correctness (NOT recycling): replacing content with a DIFFERENT-sized element must re-measure + resize the
    // presenter. The recycling optimisation must not break this.
    [Test]
    public void ContentReplace_DifferentSizedElement_ResizesPresenter()
    {
        var cp = new ContentPresenter { Content = new Border { Width = 10, Height = 10 } };
        cp.Measure(Size.Infinity);
        Assert.That(cp.DesiredSize, Is.EqualTo(new Size(10, 10)));

        cp.Content = new Border { Width = 50, Height = 40 };
        cp.Measure(Size.Infinity);
        Assert.That(cp.DesiredSize, Is.EqualTo(new Size(50, 40)),
            "replacing content with a bigger element must re-measure and resize the presenter");
    }
}

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

    // WPF-style DataContext adoption for DATA content rendered via a template - the crux that binds a data-bound
    // TabControl body (PART_SelectedContentHost) to its own selected item view-model, not the TabControl's DataContext.
    private sealed class ItemVm { public string Name { get; init; } }

    private sealed class NameTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, AdamantiumComponent container) =>
            new(() =>
            {
                var tb = new TextBlock();
                var r = new TemplateResult { RootComponent = tb };
                r.AddBinding(tb, "Text", new Binding("Name"));   // resolves against the presenter's DataContext
                return r;
            });
    }

    [Test]
    public void DataContent_BecomesDataContext_SoTemplateBindsToIt()
    {
        var vm = new ItemVm { Name = "Zed" };
        var cp = new ContentPresenter
        {
            // Ambient context is deliberately something ELSE (mirrors PART_SelectedContentHost inheriting a TabControl's).
            DataContext = new ItemVm { Name = "Ambient" },
            ContentTemplateSelector = new NameTemplateSelector(),
            Content = vm
        };
        cp.Measure(new Size(200, 100));
        cp.Arrange(new Rect(0, 0, 200, 100));

        Assert.Multiple(() =>
        {
            Assert.That(cp.DataContext, Is.SameAs(vm), "data content becomes the presenter's own DataContext");
            var tb = cp.VisualChildren.OfType<TextBlock>().First();
            Assert.That(tb.Text, Is.EqualTo("Zed"), "the built template's {Binding Name} resolves against the content view-model");
        });
    }

    // DIAGNOSTIC: mirrors exactly what the AUML CODE GENERATOR emits for a {Binding} inside a DataTemplate - it calls
    // element.SetBinding(...) in the builder, NOT result.AddBinding(...). If this fails while the AddBinding test above
    // passes, the generator must switch to result.AddBinding inside template builders (empty tab headers repro).
    [Test]
    public void ContentTemplate_SetBindingInBuilder_ResolvesAgainstDataContext()
    {
        var vm = new ItemVm { Name = "Zed" };
        var template = new DataTemplate(() =>
        {
            var tb = new TextBlock();
            var r = new TemplateResult { RootComponent = tb };
            tb.SetBinding("Text", new Binding("Name"));   // <-- the codegen path (element.SetBinding, not result.AddBinding)
            return r;
        });
        var cp = new ContentPresenter { DataContext = new ItemVm { Name = "Ambient" }, ContentTemplate = template, Content = vm };
        cp.Measure(new Size(200, 100));
        cp.Arrange(new Rect(0, 0, 200, 100));

        var tb = cp.VisualChildren.OfType<TextBlock>().First();
        Assert.That(tb.Text, Is.EqualTo("Zed"), "a {Binding} set via element.SetBinding inside a template must still resolve");
    }

    [Test]
    public void UIElementContent_DoesNotOverrideDataContext()
    {
        var ambient = new ItemVm { Name = "Ambient" };
        var cp = new ContentPresenter { DataContext = ambient };

        cp.Content = new Border();   // a UI element brings its own bindings; must keep inheriting the ambient context

        Assert.That(cp.DataContext, Is.SameAs(ambient), "a UI-element content must not hijack the presenter's DataContext");
    }
}
