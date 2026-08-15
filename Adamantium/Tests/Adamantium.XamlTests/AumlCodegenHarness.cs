using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Adamantium.UI.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Adamantium.XamlTests;

/// <summary>
/// Drives the real <see cref="AumlCodeBehindGenerator"/> (parser -> RoslynTypeResolver -> transformer ->
/// AumlSourceGenerator) over an in-memory .auml file and hands back the emitted C#. This is the only way to verify
/// codegen in-process, so every codegen fixture shares it rather than standing up its own.
/// </summary>
internal static class AumlCodegenHarness
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

    public const string WindowHeader =
        "<Window x:Namespace=\"Test.App\" " +
        "xmlns=\"http://adamantium/ui\" " +
        "xmlns:x=\"http://adamantium/ui/xaml/extensions\" ";

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

    public static string Errors(IReadOnlyList<Diagnostic> errors) =>
        "generator reported errors: " + string.Join(" | ", errors.Select(e => e.GetMessage()));

    /// <summary>The generator's WARNINGS - what it accepted but thinks you did not mean.</summary>
    public static IReadOnlyList<string> Warnings(string auml)
    {
        var result = Driver(auml).RunGenerators(Compilation()).GetRunResult();
        return result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .Select(d => d.GetMessage())
            .ToArray();
    }

    public static string Generate(string auml, out IReadOnlyList<Diagnostic> errors)
    {
        var result = Driver(auml).RunGenerators(Compilation()).GetRunResult();
        errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        return string.Join("\n\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
    }

    /// <summary>Generates and then COMPILES the output, returning compile errors - proves the emitted code actually
    /// matches the real API, not just the expected text.</summary>
    public static IReadOnlyList<Diagnostic> Compile(string auml)
    {
        Driver(auml).RunGeneratorsAndUpdateCompilation(Compilation(), out var output, out _);
        return output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    }

    private static CSharpCompilation Compilation()
    {
        _ = _seed.Length;   // touch the seed so the assemblies are loaded

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        return CSharpCompilation.Create("AumlCodegenProbe",
            syntaxTrees: null,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpGeneratorDriver Driver(string auml)
    {
        var optionsProvider = new DictOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "Test.App",
            ["build_property.projectdir"] = @"C:\Test\",
        });

        return CSharpGeneratorDriver.Create(
            generators: [new AumlCodeBehindGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText(@"C:\Test\MainWindow.auml", auml)],
            parseOptions: null,
            optionsProvider: optionsProvider);
    }
}
