using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// {Ancestor} / {Self} binding extensions. These drive the LOGICAL walk (Logical=true) so a headless test can build the
/// tree with AddLogicalChild; the visual walk shares the same FindAncestor logic and is exercised end-to-end by the app
/// (the title-bar command icons). Covers the paths that had no coverage: is-a match, Skip, Stop, Name, {Self}, TwoWay,
/// live re-resolve on (re)attach, and the diagnostic Status.
/// </summary>
[TestFixture]
public class AncestorBindingTests
{
    [Test]
    public void Ancestor_MatchesByBaseType_IsA()
    {
        var panel = new StackPanel { Width = 111 };   // StackPanel is-a Panel
        var leaf = new TextBlock();
        panel.AddLogicalChild(leaf);

        var e = new Ancestor { AncestorType = typeof(Panel), Path = "Width", Logical = true }.Apply(leaf, "Width");

        Assert.That(leaf.Width, Is.EqualTo(111d));
        Assert.That(e.Status, Is.EqualTo(BindingStatus.Active));
    }

    [Test]
    public void Ancestor_Skip_BindsTheOuterMatch()
    {
        var outer = new StackPanel { Width = 200 };
        var inner = new StackPanel { Width = 100 };
        var leaf = new TextBlock();
        inner.AddLogicalChild(leaf);
        outer.AddLogicalChild(inner);

        new Ancestor { AncestorType = typeof(StackPanel), Path = "Width", Skip = 1, Logical = true }.Apply(leaf, "Width");

        Assert.That(leaf.Width, Is.EqualTo(200d));   // skipped the nearest (inner), bound the outer
    }

    [Test]
    public void Ancestor_Stop_ResolvesNothing_WhenBoundaryHitFirst()
    {
        var stack = new StackPanel { Width = 300 };
        var boundary = new Grid();                    // Stop=Grid sits between the leaf and the StackPanel
        var leaf = new TextBlock { Width = 7 };
        boundary.AddLogicalChild(leaf);
        stack.AddLogicalChild(boundary);

        var e = new Ancestor { AncestorType = typeof(StackPanel), Path = "Width", Stop = typeof(Grid), Logical = true }.Apply(leaf, "Width");

        Assert.That(leaf.Width, Is.EqualTo(7d));      // boundary hit first -> unbound, keeps its own value
        Assert.That(e.Status, Is.EqualTo(BindingStatus.PathError));
    }

    [Test]
    public void Ancestor_Name_MatchesNamedAncestorOnly()
    {
        var outer = new StackPanel { Width = 200, Name = "target" };
        var inner = new StackPanel { Width = 100 };   // no name -> not a match
        var leaf = new TextBlock();
        inner.AddLogicalChild(leaf);
        outer.AddLogicalChild(inner);

        new Ancestor { AncestorType = typeof(StackPanel), Path = "Width", Name = "target", Logical = true }.Apply(leaf, "Width");

        Assert.That(leaf.Width, Is.EqualTo(200d));    // skipped the unnamed inner, bound the named outer
    }

    [Test]
    public void Ancestor_ReResolves_WhenAttachedAfterApply()
    {
        var panel = new StackPanel { Width = 55 };
        var leaf = new TextBlock();

        var e = new Ancestor { AncestorType = typeof(StackPanel), Path = "Width", Logical = true }.Apply(leaf, "Width");
        Assert.That(e.Status, Is.EqualTo(BindingStatus.NotAttached));   // no ancestor yet - quiet, waiting for attach

        panel.AddLogicalChild(leaf);                                    // attach -> re-resolves

        Assert.That(leaf.Width, Is.EqualTo(55d));
        Assert.That(e.Status, Is.EqualTo(BindingStatus.Active));
    }

    [Test]
    public void Ancestor_ReResolves_OnReparent()
    {
        var p1 = new StackPanel { Width = 10 };
        var p2 = new StackPanel { Width = 20 };
        var leaf = new TextBlock();
        p1.AddLogicalChild(leaf);

        new Ancestor { AncestorType = typeof(StackPanel), Path = "Width", Logical = true }.Apply(leaf, "Width");
        Assert.That(leaf.Width, Is.EqualTo(10d));

        p1.RemoveLogicalChild(leaf);
        p2.AddLogicalChild(leaf);                     // moved under a new ancestor -> re-resolves live

        Assert.That(leaf.Width, Is.EqualTo(20d));
    }

    [Test]
    public void Ancestor_LiveSourceChange_Propagates()
    {
        var panel = new StackPanel { Width = 1 };
        var leaf = new TextBlock();
        panel.AddLogicalChild(leaf);

        new Ancestor { AncestorType = typeof(StackPanel), Path = "Width", Logical = true }.Apply(leaf, "Width");
        Assert.That(leaf.Width, Is.EqualTo(1d));

        panel.Width = 42;
        BindingUpdateQueue.Flush();                   // source changes are batched per frame - flush to apply now
        Assert.That(leaf.Width, Is.EqualTo(42d));
    }

    [Test]
    public void Ancestor_TwoWay_WritesBackToSource()
    {
        var panel = new StackPanel { Width = 5 };
        var leaf = new TextBlock();
        panel.AddLogicalChild(leaf);

        new Ancestor { AncestorType = typeof(StackPanel), Path = "Width", Mode = BindingMode.TwoWay, Logical = true }.Apply(leaf, "Width");
        Assert.That(leaf.Width, Is.EqualTo(5d));

        leaf.Width = 99;                              // TwoWay: pushes back to the ancestor
        Assert.That(panel.Width, Is.EqualTo(99d));
    }

    [Test]
    public void Ancestor_Status_PathError_WhenNoMatchingAncestor()
    {
        var panel = new StackPanel();                 // NOT a Grid
        var leaf = new TextBlock();
        panel.AddLogicalChild(leaf);

        var e = new Ancestor { AncestorType = typeof(Grid), Path = "Width", Logical = true }.Apply(leaf, "Width");

        Assert.That(e.Status, Is.EqualTo(BindingStatus.PathError));   // rooted but no Grid ancestor -> flagged, not silent
    }

    [Test]
    public void Self_BindsToAnotherPropertyOnSameElement()
    {
        var leaf = new TextBlock { Height = 50 };

        var e = new Self { Path = "Height" }.Apply(leaf, "Width");

        Assert.That(leaf.Width, Is.EqualTo(50d));
        Assert.That(e.Status, Is.EqualTo(BindingStatus.Active));
    }

    [Test]
    public void Self_LiveSourceChange_Propagates()
    {
        var leaf = new TextBlock { Height = 1 };
        new Self { Path = "Height" }.Apply(leaf, "Width");
        Assert.That(leaf.Width, Is.EqualTo(1d));

        leaf.Height = 33;
        BindingUpdateQueue.Flush();
        Assert.That(leaf.Width, Is.EqualTo(33d));
    }

    [Test]
    public void Ancestor_Converter_TransformsValue()
    {
        var panel = new StackPanel { Width = 10 };
        var leaf = new TextBlock();
        panel.AddLogicalChild(leaf);

        new Ancestor { AncestorType = typeof(StackPanel), Path = "Width", Converter = new DoublingConverter(), Logical = true }
            .Apply(leaf, "Width");

        Assert.That(leaf.Width, Is.EqualTo(20d));   // 10 * 2 via the converter
    }

    [Test]
    public void Ancestor_FallbackValue_UsedWhenNoAncestor()
    {
        var panel = new StackPanel();               // no Grid ancestor
        var leaf = new TextBlock();
        panel.AddLogicalChild(leaf);

        new Ancestor { AncestorType = typeof(Grid), Path = "Width", FallbackValue = "42", Logical = true }
            .Apply(leaf, "Width");

        Assert.That(leaf.Width, Is.EqualTo(42d));    // fallback string coerced to the double target
    }

    [Test]
    public void Ancestor_DottedPath_ReadsNestedValue()
    {
        var vm = new Vm { Name = "Alice" };
        var panel = new StackPanel { DataContext = vm };
        var leaf = new TextBlock();
        panel.AddLogicalChild(leaf);

        new Ancestor { AncestorType = typeof(StackPanel), Path = "DataContext.Name", Logical = true }.Apply(leaf, "Text");

        Assert.That(leaf.Text, Is.EqualTo("Alice"));
    }

    [Test]
    public void Ancestor_DottedPath_LiveLeafChange_Propagates()
    {
        var vm = new Vm { Name = "Alice" };
        var panel = new StackPanel { DataContext = vm };
        var leaf = new TextBlock();
        panel.AddLogicalChild(leaf);
        new Ancestor { AncestorType = typeof(StackPanel), Path = "DataContext.Name", Logical = true }.Apply(leaf, "Text");
        Assert.That(leaf.Text, Is.EqualTo("Alice"));

        vm.Name = "Bob";                             // leaf-owner (the VM) INotifyPropertyChanged is observed
        BindingUpdateQueue.Flush();
        Assert.That(leaf.Text, Is.EqualTo("Bob"));
    }

    [Test]
    public void Ancestor_TargetNullValue_UsedWhenResolvedValueIsNull()
    {
        var vm = new Vm { Name = null };
        var panel = new StackPanel { DataContext = vm };
        var leaf = new TextBlock();
        panel.AddLogicalChild(leaf);

        new Ancestor { AncestorType = typeof(StackPanel), Path = "DataContext.Name", TargetNullValue = "none", Logical = true }
            .Apply(leaf, "Text");

        Assert.That(leaf.Text, Is.EqualTo("none"));
    }

    [Test]
    public void Ancestor_AsStyleSetterValue_Applies()
    {
        var panel = new StackPanel { Width = 77 };
        var leaf = new TextBlock();
        panel.AddLogicalChild(leaf);

        // A Style setter whose value is {Ancestor ...}: Setter.Apply must wire the relative binding (not treat it as a literal).
        var setter = new Setter("Width", new Ancestor { AncestorType = typeof(StackPanel), Path = "Width", Logical = true });
        setter.Apply(leaf, null, null);

        Assert.That(leaf.Width, Is.EqualTo(77d));
    }

    [Test]
    public void Self_AsStyleSetterValue_Applies()
    {
        var leaf = new TextBlock { Height = 21 };

        var setter = new Setter("Width", new Self { Path = "Height" });
        setter.Apply(leaf, null, null);

        Assert.That(leaf.Width, Is.EqualTo(21d));
    }

    private sealed class DoublingConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is double d ? d * 2 : value;
        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is double d ? d / 2 : value;
    }

    private sealed class Vm : System.ComponentModel.INotifyPropertyChanged
    {
        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Name))); }
        }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
}
