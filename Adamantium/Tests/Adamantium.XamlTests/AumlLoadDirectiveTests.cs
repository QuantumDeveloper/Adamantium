using System.Linq;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// <c>x:Load</c> holds an element back: while the condition is false NOTHING under it is constructed - it is an absent
/// element, not a hidden one. So the generator must move the element's whole build into a factory and leave a slot in
/// its place, and the condition must be allowed to be a BINDING, which is why the slot is in the logical tree at all.
/// </summary>
[TestFixture]
public class AumlLoadDirectiveTests
{
    private static string WindowWith(string child) =>
        AumlCodegenHarness.WindowHeader + "><Grid>" + child + "</Grid></Window>";

    [Test]
    public void WithoutTheDirective_TheElementIsBuiltInline()
    {
        var code = AumlCodegenHarness.Generate(WindowWith("<Border/>"), out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Not.Contain("LoadSlot"), "building eagerly is the default and must stay it");
    }

    [Test]
    public void LoadFalse_MovesTheBuildIntoAFactoryAndConstructsNothing()
    {
        var code = AumlCodegenHarness.Generate(WindowWith("<Border x:Load=\"False\"/>"), out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Contain("LoadSlot"), "a slot has to stand in for the element");
        Assert.That(code, Does.Contain("Build_load"), "and the element's build has to move into a factory");
        Assert.That(code, Does.Not.Contain(".Condition = true"), "False must not turn the condition on");
    }

    // Legal, but it arranges nothing - and a directive that reads as an arrangement and makes none is what nobody
    // notices. It still builds, so this is a warning and not an error.
    [Test]
    public void LoadTrue_BuildsAnywayAndSaysTheDirectiveIsPointless()
    {
        var code = AumlCodegenHarness.Generate(WindowWith("<Border x:Load=\"True\"/>"), out var errors);
        var warnings = AumlCodegenHarness.Warnings(WindowWith("<Border x:Load=\"True\"/>"));

        Assert.That(errors, Is.Empty, "True is legal - it must not fail the build");
        Assert.That(code, Does.Contain(".Condition = true"), "and the element still has to end up built");
        Assert.That(string.Join(" ", warnings), Does.Contain("holds nothing back"));
    }

    [Test]
    public void ABoundCondition_GoesThroughTheOrdinaryBindingPath()
    {
        var code = AumlCodegenHarness.Generate(
            WindowWith("<Border x:Load=\"{Binding IsAdvancedShown}\"/>"), out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Contain("SetBinding(global::Adamantium.UI.Controls.LoadSlot.ConditionProperty"),
            "the condition is a normal bindable property - there must be no second way to write one");
        Assert.That(code, Does.Contain("IsAdvancedShown"));
    }

    [Test]
    public void TheSlotIsALogicalChildOnly()
    {
        var code = AumlCodegenHarness.Generate(WindowWith("<Border x:Load=\"False\"/>"), out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Contain("AddLogicalChild"),
            "the slot needs the logical tree for the condition's DataContext");
        Assert.That(code, Does.Not.Contain("AddVisualChild"),
            "but layout and rendering must never see it - it is not a placeholder");
    }

    [Test]
    public void AValueThatIsNeitherLiteralNorBinding_FailsTheBuild()
    {
        AumlCodegenHarness.Generate(WindowWith("<Border x:Load=\"Maybe\"/>"), out var errors);

        Assert.That(errors, Is.Not.Empty, "a value read as the default in silence is the failure mode this prevents");
        Assert.That(string.Join(" ", errors.Select(e => e.GetMessage())), Does.Contain("x:Load"));
    }

    [Test]
    public void ANamedHeldBackElement_IsReachedThroughItsSlot_NotANullField()
    {
        var code = AumlCodegenHarness.Generate(
            WindowWith("<Border x:Name=\"Advanced\" x:Load=\"False\"/>"), out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Contain("_load_Advanced"), "it has to read through the element's own slot");
        Assert.That(code, Does.Contain("public"), "and be reachable: a view has no code behind it, so an accessor " +
            "nobody outside can call leaves x:Load=\"False\" with no way to be asked for");
        Assert.That(code, Does.Match(@"public [\w\.]+ Advanced => "),
            "the name has to be an accessor - a plain field would be null until something loaded the element");
    }

    // A trigger reaches a part by NAME, and a held-back element registers its name only once built - so a trigger aimed
    // into one does not fail, it never arrives. That silence is what this refuses.
    [Test]
    public void ATriggerAimedIntoAHeldBackElement_FailsTheBuild()
    {
        var auml = AumlCodegenHarness.WindowHeader + "><Window.Template><ControlTemplate TargetType=\"Window\">" +
                   "<Grid><Border x:Name=\"Advanced\" x:Load=\"False\"/></Grid>" +
                   "<ControlTemplate.Triggers>" +
                   "<Trigger Property=\"IsEnabled\" Value=\"True\">" +
                   "<Setter TargetName=\"Advanced\" Property=\"Width\" Value=\"100\"/>" +
                   "</Trigger>" +
                   "</ControlTemplate.Triggers></ControlTemplate></Window.Template></Window>";

        AumlCodegenHarness.Generate(auml, out var errors);

        Assert.That(errors, Is.Not.Empty, "aiming a trigger into an element that is not there must not be silent");
        Assert.That(string.Join(" ", errors.Select(e => e.GetMessage())), Does.Contain("x:Load"));
    }

    // A ControlTemplate is applied to MANY controls, and its names live in the template's own namescope rather than as
    // fields on a class - so whatever holds a held-back part cannot be one field shared by every control the template
    // was applied to.
    [Test]
    public void InsideATemplate_ItIsRefusedRatherThanGeneratedWrong()
    {
        var auml = AumlCodegenHarness.WindowHeader + "><Window.Template><ControlTemplate TargetType=\"Window\">" +
                   "<Grid><Border x:Name=\"Part\" x:Load=\"False\"/></Grid>" +
                   "</ControlTemplate></Window.Template></Window>";

        AumlCodegenHarness.Generate(auml, out var errors);

        Assert.That(string.Join(" | ", errors.Select(e => e.GetMessage())), Does.Contain("x:Load is not supported inside a template"),
            "one slot would be shared by every control the template is applied to - the name would answer with whichever was templated last");
    }

    [Test]
    public void TheGeneratedCodeCompiles()
    {
        var errors = AumlCodegenHarness.Compile(
            WindowWith("<Border x:Name=\"Advanced\" x:Load=\"{Binding IsAdvancedShown}\"/>"));

        Assert.That(errors, Is.Empty,
            "generated code did not compile: " + string.Join(" | ", errors.Select(d => d.ToString())));
    }
}
