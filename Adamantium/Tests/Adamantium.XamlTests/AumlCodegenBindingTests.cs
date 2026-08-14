using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>What the AUML code generator emits for bindings, view models, transitions and trigger animations - driven
/// through <see cref="AumlCodegenHarness"/>.</summary>
[TestFixture]
public class AumlCodegenBindingTests
{
    private static string Generate(string auml, out IReadOnlyList<Diagnostic> errors) => AumlCodegenHarness.Generate(auml, out errors);

    private static IReadOnlyList<Diagnostic> Compile(string auml) => AumlCodegenHarness.Compile(auml);

    private const string WindowHeader = AumlCodegenHarness.WindowHeader;

    private static string Errors(IReadOnlyList<Diagnostic> errors) => AumlCodegenHarness.Errors(errors);

    [Test]
    public void Harness_GeneratesControl_WithoutErrors()
    {
        var code = Generate(WindowHeader + "Title=\"Hello\"><Grid /></Window>", out var errors);

        Assert.That(errors, Is.Empty, Errors(errors));
        Assert.That(code, Does.Contain("class MainWindow"));
        Assert.That(code, Does.Contain("InitializeComponent"));
    }

    [Test]
    public void Binding_EmitsSetBinding()
    {
        var code = Generate(WindowHeader + "Title=\"{Binding WindowTitle}\"><Grid /></Window>", out var errors);

        Assert.That(errors, Is.Empty, Errors(errors));
        Assert.That(code, Does.Contain("SetBinding(\"Title\""));
        Assert.That(code, Does.Contain($"new global::Adamantium.UI.Core.Data.Binding()"));
        Assert.That(code, Does.Contain("PropertyPath(\"WindowTitle\")"));
    }

    [Test]
    public void NestedMultiBinding_EmitsNestedMultiBindings()
    {
        var auml = WindowHeader + ">" +
                   "<Window.Title>" +
                   "<MultiBinding>" +
                   "<Binding Path=\"A\" />" +
                   "<MultiBinding><Binding Path=\"B\" /><Binding Path=\"C\" /></MultiBinding>" +
                   "</MultiBinding>" +
                   "</Window.Title>" +
                   "<Grid />" +
                   "</Window>";

        var code = Generate(auml, out var errors);

        Assert.That(errors, Is.Empty, Errors(errors));
        Assert.That(code, Does.Contain("SetBinding(\"Title\""));
        Assert.That(code, Does.Contain(".Bindings.Add("));
        var multiBindings = Regex.Matches(code, @"new global::Adamantium\.UI\.Core\.Data\.MultiBinding\(\)").Count;
        Assert.That(multiBindings, Is.GreaterThanOrEqualTo(2), "expected an outer and a nested MultiBinding");
    }

    [Test]
    public void Binding_GeneratedCodeCompiles()
    {
        var errors = Compile(WindowHeader + "Title=\"{Binding WindowTitle}\"><Grid /></Window>");
        Assert.That(errors, Is.Empty, "generated code did not compile: " + string.Join(" | ", errors.Select(d => d.ToString())));
    }

    [Test]
    public void ViewModel_EmitsViewModelTypeOverride()
    {
        // x:ViewModel binds a view-model type to the view; for codegen any resolvable type works, so TextBlock
        // stands in for a VM. The generated class declares the type as a ViewModelType override (pure metadata) -
        // the framework resolves the instance from the DI resolver at attach time, not in generated code.
        var code = Generate(WindowHeader + "x:ViewModel=\"TextBlock\"><Grid /></Window>", out var errors);

        Assert.That(errors, Is.Empty, Errors(errors));
        Assert.That(code, Does.Contain("public override global::System.Type ViewModelType => typeof("));
        Assert.That(code, Does.Contain("TextBlock"));
    }

    [Test]
    public void ViewModel_GeneratedCodeCompiles()
    {
        var errors = Compile(WindowHeader + "x:ViewModel=\"TextBlock\"><Grid /></Window>");
        Assert.That(errors, Is.Empty, "generated code did not compile: " + string.Join(" | ", errors.Select(d => d.ToString())));
    }

    [Test]
    public void Transition_EmitsCollectionItemAndTimeSpanDuration()
    {
        var code = Generate(WindowHeader +
            "><Grid><Grid.Transitions><DoubleTransition Property=\"Width\" Duration=\"0:0:0.3\"/></Grid.Transitions></Grid></Window>",
            out var errors);

        Assert.That(errors, Is.Empty, Errors(errors));
        Assert.That(code, Does.Contain("DoubleTransition"), "the transition element is instantiated");
        Assert.That(code, Does.Contain("Property = \"Width\""), "Property is a plain string name");
        Assert.That(code, Does.Contain("0:0:0.3"), "Duration text is passed to the TimeSpan parser");
        Assert.That(code, Does.Contain(".Add("), "the transition is added to the Transitions collection");
    }

    [Test]
    public void TriggerAnimation_GeneratedCodeCompiles()
    {
        // The full declarative chain: a template trigger whose EnterActions start a keyframe Animation.
        var auml = WindowHeader + "><Button><Button.Template>" +
                   "<ControlTemplate TargetType=\"Button\">" +
                   "<Grid />" +
                   "<ControlTemplate.Triggers>" +
                   "<PropertyTrigger Property=\"IsMouseOver\" Value=\"true\">" +
                   "<PropertyTrigger.EnterActions>" +
                   "<RunAnimationAction>" +
                   "<Animation Duration=\"0:0:0.3\" Easing=\"CubicOut\">" +
                   "<KeyFrame Cue=\"0%\"><Setter Property=\"Opacity\" Value=\"0.5\"/></KeyFrame>" +
                   "<KeyFrame Cue=\"100%\"><Setter Property=\"Opacity\" Value=\"1\"/></KeyFrame>" +
                   "</Animation>" +
                   "</RunAnimationAction>" +
                   "</PropertyTrigger.EnterActions>" +
                   "</PropertyTrigger>" +
                   "</ControlTemplate.Triggers>" +
                   "</ControlTemplate>" +
                   "</Button.Template></Button></Window>";

        var errors = Compile(auml);
        Assert.That(errors, Is.Empty, "generated code did not compile: " + string.Join(" | ", errors.Select(d => d.ToString())));
    }

    [Test]
    public void LogicalTriggerAnimation_GeneratedCodeCompiles()
    {
        // Triggers declared directly on a control (the logical layer) whose EnterActions run a keyframe Animation.
        var auml = WindowHeader + "><Button><Button.Triggers>" +
                   "<PropertyTrigger Property=\"IsMouseOver\" Value=\"true\">" +
                   "<PropertyTrigger.EnterActions>" +
                   "<RunAnimationAction>" +
                   "<Animation Duration=\"0:0:0.2\" Easing=\"CubicOut\">" +
                   "<KeyFrame Cue=\"0%\"><Setter Property=\"Opacity\" Value=\"0.5\"/></KeyFrame>" +
                   "<KeyFrame Cue=\"100%\"><Setter Property=\"Opacity\" Value=\"1\"/></KeyFrame>" +
                   "</Animation>" +
                   "</RunAnimationAction>" +
                   "</PropertyTrigger.EnterActions>" +
                   "</PropertyTrigger>" +
                   "</Button.Triggers></Button></Window>";

        var errors = Compile(auml);
        Assert.That(errors, Is.Empty, "generated code did not compile: " + string.Join(" | ", errors.Select(d => d.ToString())));
    }

    [Test]
    public void Transition_GeneratedCodeCompiles()
    {
        // Proves the whole chain compiles against the real API: Transitions collection (new + Add), DoubleTransition,
        // string Property, and TimeSpan Duration via the registered TimeSpanParser.
        var errors = Compile(WindowHeader +
            "><Grid><Grid.Transitions><DoubleTransition Property=\"Width\" Duration=\"0:0:0.3\"/></Grid.Transitions></Grid></Window>");
        Assert.That(errors, Is.Empty, "generated code did not compile: " + string.Join(" | ", errors.Select(d => d.ToString())));
    }

    [Test]
    public void ViewModel_AcceptsXTypeMarkupExtensionForm()
    {
        // x:ViewModel="{x:Type ...}" must work too (lets the editor complete types in the braces).
        var code = Generate(WindowHeader + "x:ViewModel=\"{x:Type TextBlock}\"><Grid /></Window>", out var errors);

        Assert.That(errors, Is.Empty, Errors(errors));
        Assert.That(code, Does.Contain("public override global::System.Type ViewModelType => typeof("));
        Assert.That(code, Does.Contain("TextBlock"));
    }

    private const string ItemsControlWithItemTemplate =
        WindowHeader + ">" +
        "<ItemsControl ItemsSource=\"{Binding People}\">" +
        "<ItemsControl.ItemTemplate>" +
        "<DataTemplate><TextBlock Text=\"{Binding Name}\"/></DataTemplate>" +
        "</ItemsControl.ItemTemplate>" +
        "</ItemsControl>" +
        "</Window>";

    [Test]
    public void ItemTemplate_EmitsDataTemplateBuilder()
    {
        // Regression: an inline <DataTemplate> must be emitted with a Func<TemplateResult> builder (like ControlTemplate),
        // NOT a bare `new DataTemplate()` (whose Build() NRE'd - no builder, no container).
        var code = Generate(ItemsControlWithItemTemplate, out var errors);

        Assert.That(errors, Is.Empty, Errors(errors));
        Assert.Multiple(() =>
        {
            Assert.That(code, Does.Contain("DataTemplate(Build_"), "DataTemplate must take a builder method");
            Assert.That(code, Does.Contain("result.RootComponent ="), "the builder sets the template root");
            Assert.That(code, Does.Match(@"new [\w\.:]*DataTemplate\(Build_"), "builder ctor, not the parameterless one");
            Assert.That(code, Does.Not.Match(@"new [\w\.:]*DataTemplate\(\s*\)"), "no empty `new DataTemplate()` (that was the NRE)");
            Assert.That(code, Does.Contain("SetBinding(\"Text\""), "the item template's {Binding Name} is emitted");
        });
    }

    [Test]
    public void ItemTemplate_GeneratedCodeCompiles()
    {
        var errors = Compile(ItemsControlWithItemTemplate);
        Assert.That(errors, Is.Empty, "generated code did not compile: " + string.Join(" | ", errors.Select(d => d.ToString())));
    }

    [Test]
    public void NestedMultiBinding_GeneratedCodeCompiles()
    {
        var auml = WindowHeader + ">" +
                   "<Window.Title>" +
                   "<MultiBinding>" +
                   "<Binding Path=\"A\" />" +
                   "<MultiBinding><Binding Path=\"B\" /><Binding Path=\"C\" /></MultiBinding>" +
                   "</MultiBinding>" +
                   "</Window.Title>" +
                   "<Grid />" +
                   "</Window>";

        var errors = Compile(auml);
        Assert.That(errors, Is.Empty, "generated code did not compile: " + string.Join(" | ", errors.Select(d => d.ToString())));
    }
}
