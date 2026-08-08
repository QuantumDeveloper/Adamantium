using System;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A value written as an ATTRIBUTE and the identical value arriving through a <c>{Binding}</c> land on the
/// property the same way - both convert through the engine's TypeParser registry, which is what WPF reaches with a
/// TypeConverter. <c>Convert.ChangeType</c> alone makes neither a Geometry out of a path nor a Brush out of a name.</summary>
[TestFixture]
public class BindingStringConversionTests
{
    private sealed class Source
    {
        public string Path { get; init; }
        public string Colour { get; init; }
        public string Edges { get; init; }
        public string Number { get; init; }
        public string Mode { get; init; }
    }

    // Path data in a view model, bound onto a geometry-typed property.
    [Test]
    public void AStringBindsOntoAGeometryProperty()
    {
        var path = new Path { DataContext = new Source { Path = "M0,0 L10,10 L0,10 Z" } };
        path.SetBinding(nameof(Path.Data), new Binding(nameof(Source.Path)));

        Assert.That(path.Data, Is.Not.Null, "a path written as text is a Geometry, however it arrives");
    }

    [Test]
    public void AStringBindsOntoABrushProperty()
    {
        var border = new Border { DataContext = new Source { Colour = "Red" } };
        border.SetBinding(nameof(Border.Background), new Binding(nameof(Source.Colour)));

        Assert.That(border.Background, Is.InstanceOf<SolidColorBrush>());
    }

    [Test]
    public void AStringBindsOntoAThicknessProperty()
    {
        var border = new Border { DataContext = new Source { Edges = "4,8" } };
        border.SetBinding(nameof(Border.BorderThickness), new Binding(nameof(Source.Edges)));

        Assert.That(border.BorderThickness, Is.EqualTo(new Thickness(4, 8)));
    }

    // Convert.ChangeType handles these; the fall-through must not disturb them.
    [Test]
    public void TheConvertibleCasesAreUnchanged()
    {
        var border = new Border { DataContext = new Source { Number = "120" } };
        border.SetBinding(nameof(Border.Width), new Binding(nameof(Source.Number)));

        Assert.That(border.Width, Is.EqualTo(120.0));
    }

    [Test]
    public void AStringBindsOntoAnEnumProperty()
    {
        var border = new Border { DataContext = new Source { Mode = "Collapsed" } };
        border.SetBinding(nameof(Border.Visibility), new Binding(nameof(Source.Mode)));

        Assert.That(border.Visibility, Is.EqualTo(Visibility.Collapsed));
    }

    // A value that fits nowhere is skipped rather than pushed: the property keeps what it had and nothing throws.
    [Test]
    public void AStringThatFitsNowhere_LeavesThePropertyAlone()
    {
        var border = new Border { BorderThickness = new Thickness(3), DataContext = new Source { Edges = "not a thickness" } };

        Assert.DoesNotThrow(() => border.SetBinding(nameof(Border.BorderThickness), new Binding(nameof(Source.Edges))));
        Assert.That(border.BorderThickness, Is.EqualTo(new Thickness(3)));
    }
}
