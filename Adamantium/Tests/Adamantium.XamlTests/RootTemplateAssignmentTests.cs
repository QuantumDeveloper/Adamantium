using System.Linq;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// A template written on the ROOT itself (<c>&lt;Window.Template&gt;</c>) rather than in a setter. The assignment is
/// addressed through the current parent, and the root has none - so it came out as <c>.Template = ...</c>, with no
/// receiver at all, and the generated file did not parse. The two sibling branches beside it (attached template,
/// template-priority assignment) had always spelled the root case out; only this one had not.
/// </summary>
[TestFixture]
public class RootTemplateAssignmentTests
{
    private const string RootTemplate =
        AumlCodegenHarness.WindowHeader + "><Window.Template><ControlTemplate TargetType=\"Window\">" +
        "<Grid><Border x:Name=\"Part\"/></Grid>" +
        "</ControlTemplate></Window.Template></Window>";

    [Test]
    public void ATemplateOnTheRootIsAssignedToTheRoot()
    {
        var code = AumlCodegenHarness.Generate(RootTemplate, out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Contain("this.Template = "));
        Assert.That(code, Does.Not.Contain("\n        .Template"), "an assignment with no receiver does not compile");
    }

    [Test]
    public void AndTheGeneratedCodeCompiles()
    {
        var errors = AumlCodegenHarness.Compile(RootTemplate);

        Assert.That(errors, Is.Empty,
            "generated code did not compile: " + string.Join(" | ", errors.Select(d => d.ToString())));
    }
}
