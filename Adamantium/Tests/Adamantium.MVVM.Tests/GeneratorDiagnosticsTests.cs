using System;
using System.IO;
using System.Linq;
using Adamantium.MVVM.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Adamantium.MVVM.Tests;

/// <summary>
/// Diagnostics are driven by hand (not as compiled fixtures): AMVVM002 is an Error, so a nested VM placed in this
/// project's own source would break the build. We run the generator over inline source and assert on its diagnostics.
/// </summary>
[TestFixture]
public class GeneratorDiagnosticsTests
{
    private const string Attributes = @"
namespace Adamantium.MVVM
{
    [System.AttributeUsage(System.AttributeTargets.Field)]  public sealed class BindableAttribute : System.Attribute {}
    [System.AttributeUsage(System.AttributeTargets.Method)] public sealed class CommandAttribute : System.Attribute {}
    [System.AttributeUsage(System.AttributeTargets.Class)]  public sealed class ViewModelAttribute : System.Attribute {}
}";

    private static System.Collections.Generic.IList<Diagnostic> RunGenerator(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Where(p => p.Length > 0)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray();

        var compilation = CSharpCompilation.Create("DiagProbe",
            new[] { CSharpSyntaxTree.ParseText(Attributes), CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new MvvmGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        return diagnostics;
    }

    [Test]
    public void NestedType_ReportsAmvvm002Error()
    {
        const string source = @"
namespace Demo
{
    public class Outer
    {
        [Adamantium.MVVM.ViewModel]
        public partial class InnerVm
        {
            [Adamantium.MVVM.Bindable] private int _count;
        }
    }
}";
        var diagnostics = RunGenerator(source);

        var amvvm002 = diagnostics.Where(d => d.Id == "AMVVM002").ToArray();
        Assert.That(amvvm002, Is.Not.Empty, "nested MVVM type produced no AMVVM002");
        Assert.That(amvvm002.All(d => d.Severity == DiagnosticSeverity.Error), "AMVVM002 must be an Error");
        Assert.That(amvvm002.Any(d => d.GetMessage().Contains("InnerVm")), "diagnostic should name the offending type");
    }

    [Test]
    public void ValidationWithoutValidatingBase_ReportsAmvvm003Warning()
    {
        const string source = @"
namespace Demo
{
    [Adamantium.MVVM.ViewModel]
    public partial class FormVm
    {
        [Adamantium.MVVM.Bindable]
        [System.ComponentModel.DataAnnotations.Required]
        private string _name;
    }
}";
        var diagnostics = RunGenerator(source);

        var amvvm003 = diagnostics.Where(d => d.Id == "AMVVM003").ToArray();
        Assert.That(amvvm003, Is.Not.Empty, "validation attribute on a non-validating VM produced no AMVVM003");
        Assert.That(amvvm003.All(d => d.Severity == DiagnosticSeverity.Warning), "AMVVM003 must be a Warning");
    }

    [Test]
    public void TopLevelType_ReportsNoNestedError()
    {
        const string source = @"
namespace Demo
{
    [Adamantium.MVVM.ViewModel]
    public partial class TopVm
    {
        [Adamantium.MVVM.Bindable] private int _count;
    }
}";
        var diagnostics = RunGenerator(source);

        Assert.That(diagnostics.Any(d => d.Id == "AMVVM002"), Is.False, "a top-level type must not trigger AMVVM002");
    }
}
