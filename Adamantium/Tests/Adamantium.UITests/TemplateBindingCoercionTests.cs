using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// A {TemplateBinding} whose source and target types differ. The markup shorthand
// ColumnDefinitions="Auto,{TemplateBinding OverflowButtonWidth}" binds a double onto a GridLength, which only works if
// the expression converts like {Binding} does instead of writing the raw value into the slot.
[TestFixture]
public class TemplateBindingCoercionTests
{
    [Test]
    public void DoubleSourceReachesGridLengthTarget()
    {
        var column = new ColumnDefinition();
        var bar = new RibbonQuickAccess { OverflowButtonWidth = 28 };

        bar.Template = new ControlTemplate(() =>
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(column);

            var result = new TemplateResult { RootComponent = grid };
            result.AddTemplateBinding(column, "Width", new TemplateBinding { Path = "OverflowButtonWidth" });
            return result;
        });

        var root = new Border { Width = 200, Height = 40, Child = bar };
        WindowExtension.UpdateTree(root);

        Assert.That(column.Width, Is.EqualTo(new GridLength(28)));
    }

    [Test]
    public void UnfittableValueLeavesTheTargetAlone()
    {
        // No local Width here on purpose: a Local value outranks Template, and the binding's write would be masked
        // rather than skipped - the test would pass without ever exercising the coercion.
        var column = new ColumnDefinition();
        var bar = new RibbonQuickAccess { Name = "not a length" };

        bar.Template = new ControlTemplate(() =>
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(column);

            var result = new TemplateResult { RootComponent = grid };
            result.AddTemplateBinding(column, "Width", new TemplateBinding { Path = "Name" });
            return result;
        });

        var root = new Border { Width = 200, Height = 40, Child = bar };
        WindowExtension.UpdateTree(root);

        Assert.That(column.Width, Is.EqualTo(new GridLength(1, GridUnitType.Star)));
    }
}
