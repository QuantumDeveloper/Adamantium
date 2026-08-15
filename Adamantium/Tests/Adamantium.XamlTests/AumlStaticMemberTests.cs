using System.Linq;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// <c>{x:Static Type.Member}</c> reads a static field or property where the value is used. Without it a constant could
/// only reach markup by being restated as a resource, so the declaration in C# and the one in the theme drifted apart on
/// their own. The member is checked at BUILD time - a name that does not exist must fail the build, not leave a property
/// silently empty.
/// </summary>
[TestFixture]
public class AumlStaticMemberTests
{
    // A real static on a real type: RibbonApplicationMenu.DefaultRailWidth (public const double = 200).
    private const string Owner = "RibbonApplicationMenu";

    [Test]
    public void AStaticMemberIsReadWhereItIsUsed()
    {
        var code = AumlCodegenHarness.Generate(
            AumlCodegenHarness.WindowHeader + $"Width=\"{{x:Static {Owner}.DefaultRailWidth}}\"><Grid /></Window>",
            out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Contain("DefaultRailWidth"), "the member has to be read, not parsed as text");
        Assert.That(code, Does.Not.Contain("\"{x:Static"), "and certainly not left as the raw markup string");
    }

    [Test]
    public void TheGeneratedReadCompiles()
    {
        var errors = AumlCodegenHarness.Compile(
            AumlCodegenHarness.WindowHeader + $"Width=\"{{x:Static {Owner}.DefaultRailWidth}}\"><Grid /></Window>");

        Assert.That(errors, Is.Empty, "generated code did not compile: " + string.Join(" | ", errors.Select(d => d.ToString())));
    }

    // The half that makes it worth having: a typo is a build error. This is exactly what a resource-key detour could
    // never give.
    [Test]
    public void AMemberThatDoesNotExistFailsTheBuild()
    {
        AumlCodegenHarness.Generate(
            AumlCodegenHarness.WindowHeader + $"Width=\"{{x:Static {Owner}.DefaultRailWidht}}\"><Grid /></Window>",
            out var errors);

        Assert.That(errors.Select(e => e.GetMessage()), Has.Some.Contains("has no member 'DefaultRailWidht'"),
            string.Join(" | ", errors.Select(e => e.GetMessage())));
    }

    [Test]
    public void AnUnresolvableTypeFailsTheBuild()
    {
        AumlCodegenHarness.Generate(
            AumlCodegenHarness.WindowHeader + "Width=\"{x:Static NoSuchType.Member}\"><Grid /></Window>", out var errors);

        Assert.That(errors.Select(e => e.GetMessage()), Has.Some.Contains("x:Static type"),
            string.Join(" | ", errors.Select(e => e.GetMessage())));
    }

    [Test]
    public void AValueWithoutAMemberFailsTheBuild()
    {
        AumlCodegenHarness.Generate(
            AumlCodegenHarness.WindowHeader + "Width=\"{x:Static Nonsense}\"><Grid /></Window>", out var errors);

        Assert.That(errors.Select(e => e.GetMessage()), Has.Some.Contains("expects 'Type.Member'"),
            string.Join(" | ", errors.Select(e => e.GetMessage())));
    }
}
