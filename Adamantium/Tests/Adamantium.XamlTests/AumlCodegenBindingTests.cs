using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Adamantium.UI.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// Harness for the AUML compile-time code generator: it drives the real <see cref="AumlCodeBehindGenerator"/>
/// (parser -> RoslynTypeResolver -> transformer -> AumlSourceGenerator) over an in-memory .auml file and inspects the
/// emitted C#. This is the only way to verify codegen in-process, since there was no existing harness for it.
/// </summary>
[TestFixture]
public class AumlCodegenBindingTests
{
    // Force the UI assemblies to load so AppDomain.GetAssemblies() (our reference set) includes them.
    private static readonly Type[] _seed =
    [
        typeof(Adamantium.UI.Controls.Window),
        typeof(Adamantium.UI.Controls.Text.TextBlock),
        typeof(Adamantium.UI.Core.Data.Binding),
        typeof(Adamantium.UI.Core.Data.MultiBinding),
        typeof(Adamantium.Core.TypeParsing.TypeParser),   // force-load Adamantium.Core so codegen resolves OUR TypeParser
    ];

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        private readonly SourceText _text = SourceText.From(content);
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }

    private sealed class DictOptions(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string? value) => values.TryGetValue(key, out value);
    }

    private sealed class DictOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _global;
        public DictOptionsProvider(Dictionary<string, string> values) => _global = new DictOptions(values);
        public override AnalyzerConfigOptions GlobalOptions => _global;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _global;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _global;
    }

    private static string Generate(string auml, out IReadOnlyList<Diagnostic> errors)
    {
        _ = _seed.Length;   // touch the seed so the assemblies are loaded

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var compilation = CSharpCompilation.Create("AumlCodegenProbe",
            syntaxTrees: null,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var optionsProvider = new DictOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "Test.App",
            ["build_property.projectdir"] = @"C:\Test\",
        });

        var driver = CSharpGeneratorDriver.Create(
            generators: [new AumlCodeBehindGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText(@"C:\Test\MainWindow.auml", auml)],
            parseOptions: null,
            optionsProvider: optionsProvider);

        var result = driver.RunGenerators(compilation).GetRunResult();
        errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        return string.Join("\n\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
    }

    // Generates and then COMPILES the output, returning compile errors — proves the emitted binding code (SetBinding,
    // PropertyPath, MultiBinding.Bindings.Add, ...) actually matches the real API, not just the expected text.
    private static IReadOnlyList<Diagnostic> Compile(string auml)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var compilation = CSharpCompilation.Create("AumlCodegenCompile",
            syntaxTrees: null,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var optionsProvider = new DictOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "Test.App",
            ["build_property.projectdir"] = @"C:\Test\",
        });

        var driver = CSharpGeneratorDriver.Create(
            generators: [new AumlCodeBehindGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText(@"C:\Test\MainWindow.auml", auml)],
            parseOptions: null,
            optionsProvider: optionsProvider);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    }

    private const string WindowHeader =
        "<Window x:Namespace=\"Test.App\" " +
        "xmlns=\"http://adamantium/ui\" " +
        "xmlns:x=\"http://adamantium/ui/xaml/extensions\" ";

    private static string Errors(IReadOnlyList<Diagnostic> errors) =>
        "generator reported errors: " + string.Join(" | ", errors.Select(e => e.GetMessage()));

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
                   "<KeyFrame Cue=\"0\"><Setter Property=\"Opacity\" Value=\"0.5\"/></KeyFrame>" +
                   "<KeyFrame Cue=\"1\"><Setter Property=\"Opacity\" Value=\"1\"/></KeyFrame>" +
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
