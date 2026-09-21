using System;
using System.IO;
using System.Linq;
using Adamantium.MVVM.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Adamantium.MVVM.Tests;

/// <summary>How <c>[Command(CanExecute = ...)]</c> resolves the member it names. Asserted on the generated TEXT: the
/// gate is one expression, and whether it comes out as <c>() =&gt; Flag</c> or <c>() =&gt; Flag()</c> is the whole
/// question.</summary>
[TestFixture]
public class CommandCanExecuteGeneratorTests
{
    // The real attribute shapes, so CanExecute/Name can be set in the probe sources.
    private const string AttributeStubs = @"
namespace Adamantium.MVVM
{
    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public sealed class BindableAttribute : System.Attribute {}

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class CommandAttribute : System.Attribute
    {
        public string CanExecute { get; set; }
        public string Name { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class ViewModelAttribute : System.Attribute {}
}";

    private static (string Command, Diagnostic[] Diagnostics) Run(string source, string commandName)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Where(p => p.Length > 0)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray();

        var compilation = CSharpCompilation.Create("CanExecuteProbe",
            new[] { CSharpSyntaxTree.ParseText(AttributeStubs), CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MvvmGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        var generated = driver.GetRunResult().Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(s => s.HintName.Contains("." + commandName + ".Command."));

        return (generated.SourceText?.ToString(), diagnostics.ToArray());
    }

    private static string Vm(string body) => @"
namespace Demo
{
    [Adamantium.MVVM.ViewModel]
    public partial class Vm
    {" + body + @"
    }
}";

    // Both halves come out of the same generator pass, so the property does not exist as a symbol yet when the command
    // is built. Emitting a CALL here fails as CS1955 in a file nobody wrote.
    [Test]
    public void AGateNamingABindableField_IsReadAsAProperty()
    {
        var (command, diagnostics) = Run(Vm(@"
        [Adamantium.MVVM.Bindable] private bool _hasSelection;
        [Adamantium.MVVM.Command(CanExecute = nameof(HasSelection))] private void Delete() { }"), "DeleteCommand");

        Assert.Multiple(() =>
        {
            Assert.That(command, Does.Contain("() => HasSelection)"));
            Assert.That(command, Does.Not.Contain("HasSelection()"));
            Assert.That(diagnostics.Any(d => d.Id == "AMVVM004"), Is.False);
        });
    }

    // A [Bindable] PARTIAL property already exists as a symbol - the two shapes must be answered the same way.
    [Test]
    public void AGateNamingABindablePartialProperty_IsReadAsAProperty()
    {
        var (command, _) = Run(Vm(@"
        [Adamantium.MVVM.Bindable] public partial bool HasSelection { get; set; }
        [Adamantium.MVVM.Command(CanExecute = nameof(HasSelection))] private void Delete() { }"), "DeleteCommand");

        Assert.That(command, Does.Contain("() => HasSelection)"));
    }

    // GetMembers is DECLARED members only, so a gate on a base view model needs the walk.
    [Test]
    public void AnInheritedGate_IsFound()
    {
        const string source = @"
namespace Demo
{
    public class BaseVm { protected bool IsReady => true; }

    [Adamantium.MVVM.ViewModel]
    public partial class Vm : BaseVm
    {
        [Adamantium.MVVM.Command(CanExecute = nameof(IsReady))] private void Save() { }
    }
}";
        var (command, diagnostics) = Run(source, "SaveCommand");

        Assert.Multiple(() =>
        {
            Assert.That(command, Does.Contain("() => IsReady)"));
            Assert.That(diagnostics.Any(d => d.Id == "AMVVM004"), Is.False);
        });
    }

    // ...but a PRIVATE base member is unreachable from the generated partial, and saying so beats an inaccessibility
    // error in generated code.
    [Test]
    public void APrivateGateOnABase_IsNotUsable()
    {
        const string source = @"
namespace Demo
{
    public class BaseVm { private bool IsReady => true; }

    [Adamantium.MVVM.ViewModel]
    public partial class Vm : BaseVm
    {
        [Adamantium.MVVM.Command(CanExecute = nameof(IsReady))] private void Save() { }
    }
}";
        var (_, diagnostics) = Run(source, "SaveCommand");

        Assert.That(diagnostics.Any(d => d.Id == "AMVVM004"), Is.True);
    }

    [Test]
    public void AMethodGate_IsCalled()
    {
        var (command, _) = Run(Vm(@"
        private bool CanSave() => true;
        [Adamantium.MVVM.Command(CanExecute = nameof(CanSave))] private void Save() { }"), "SaveCommand");

        Assert.That(command, Does.Contain("() => CanSave())"));
    }

    // A typed command hands its argument to a gate that takes one, and ignores it for a gate that does not.
    [Test]
    public void ATypedCommand_PassesItsArgumentToAGateThatTakesOne()
    {
        var (command, _) = Run(Vm(@"
        private bool CanSave(string s) => true;
        [Adamantium.MVVM.Command(CanExecute = nameof(CanSave))] private void Save(string s) { }"), "SaveCommand");

        Assert.That(command, Does.Contain("arg => CanSave(arg))"));
    }

    [Test]
    public void ATypedCommand_IgnoresItsArgumentForAZeroArgumentGate()
    {
        var (command, _) = Run(Vm(@"
        private bool CanSave() => true;
        [Adamantium.MVVM.Command(CanExecute = nameof(CanSave))] private void Save(string s) { }"), "SaveCommand");

        Assert.That(command, Does.Contain("_ => CanSave())"));
    }

    // A command WITHOUT an argument has none to give the gate.
    [Test]
    public void AGateWantingAnArgumentTheCommandHasNot_IsRejected()
    {
        var (_, diagnostics) = Run(Vm(@"
        private bool CanSave(string s) => true;
        [Adamantium.MVVM.Command(CanExecute = nameof(CanSave))] private void Save() { }"), "SaveCommand");

        Assert.That(diagnostics.Any(d => d.Id == "AMVVM004"), Is.True);
    }

    [Test]
    public void ANonBoolGate_IsRejected()
    {
        var (_, diagnostics) = Run(Vm(@"
        [Adamantium.MVVM.Bindable] private string _title;
        [Adamantium.MVVM.Command(CanExecute = nameof(Title))] private void Save() { }"), "SaveCommand");

        Assert.That(diagnostics.Any(d => d.Id == "AMVVM004"), Is.True);
    }

    // The error names the gate, points at the ATTRIBUTE, and the command still comes out - so this is the ONE error,
    // not the first of a cascade at every binding site.
    [Test]
    public void AnUnknownGate_IsOneErrorAtTheAttribute_AndTheCommandStillComesOut()
    {
        var (command, diagnostics) = Run(Vm(@"
        [Adamantium.MVVM.Command(CanExecute = nameof(Vm))] private void Save() { }"), "SaveCommand");

        var amvvm004 = diagnostics.Where(d => d.Id == "AMVVM004").ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(amvvm004, Has.Length.EqualTo(1));
            Assert.That(amvvm004[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(amvvm004[0].GetMessage(), Does.Contain("Vm"), "names the gate that could not be resolved");
            // Somewhere in the source, not Location.None. No SourceTree: a diagnostic raised inside a transform is
            // stored as a LocationInfo (path + spans) so it cannot defeat the generator's caching.
            Assert.That(amvvm004[0].Location, Is.Not.EqualTo(Location.None), "points at the attribute, not nowhere");
            Assert.That(amvvm004[0].Location.GetLineSpan().StartLinePosition.Line, Is.GreaterThan(0));
            Assert.That(command, Does.Contain("SaveCommand"), "the command is still generated");
            Assert.That(command, Does.Not.Contain("=> Vm"), "just without a gate");
        });
    }
}
