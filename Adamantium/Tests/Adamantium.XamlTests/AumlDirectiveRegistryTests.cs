using System.Linq;
using Adamantium.UI.Markup.AST;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// <see cref="AumlDirectives"/> claims to be the single source of truth for the <c>x:</c> vocabulary: the transformer
/// judges names against it and the language server completes from it. That claim only holds if nothing can be
/// implemented without being listed, or listed without being implemented - and it had already drifted once
/// (<c>x:Null</c> worked, but no tooling knew about it). These are the tests that keep the two in step.
/// </summary>
[TestFixture]
public class AumlDirectiveRegistryTests
{
    private static string Attribute(string directive, string value = "Whatever") =>
        AumlCodegenHarness.WindowHeader + $"x:{directive}=\"{value}\"><Grid /></Window>";

    // What this test asks is "is the directive ACCEPTED", so it has to hand over a value the directive would accept:
    // one that NAMES A TYPE needs a type that resolves, and one with a fixed set of answers needs one of them -
    // otherwise the failure is about the value and says nothing about the directive being implemented.
    private static string LegalValueFor(AumlDirectiveInfo directive)
    {
        if (directive.IsTypeReference) return "TextBlock";

        return directive.Name switch
        {
            AumlDirectives.Load => "False",
            AumlDirectives.KeepAlive => "Enabled",
            _ => "Whatever"
        };
    }

    [Test]
    public void EveryListedAttributeDirectiveIsAccepted()
    {
        foreach (var directive in AumlDirectives.All.Where(d => d.Usage == AumlDirectiveUsage.Attribute))
        {
            AumlCodegenHarness.Generate(Attribute(directive.Name, LegalValueFor(directive)), out var errors);

            Assert.That(errors.Select(e => e.GetMessage()), Has.None.Contains($"x:{directive.Name}"),
                $"x:{directive.Name} is listed for tooling but the transformer does not accept it");
        }
    }

    [Test]
    public void ADirectiveNobodyListedIsRejected()
    {
        AumlCodegenHarness.Generate(Attribute("Nmae"), out var errors);

        Assert.That(errors.Select(e => e.GetMessage()), Has.Some.Contains("Unknown directive 'x:Nmae'"),
            "a typo in a directive name must fail the build, not be silently dropped");
    }

    // The two forms are not interchangeable, and saying WHICH mistake was made is the whole point of judging against the
    // registry: "you invented this" and "this one goes in a value" are different problems with different fixes.
    [Test]
    public void AValueDirectiveWrittenOnTheElementSaysSo()
    {
        AumlCodegenHarness.Generate(Attribute(AumlDirectives.Null), out var errors);

        var messages = errors.Select(e => e.GetMessage()).ToList();
        Assert.That(messages, Has.Some.Contains("belongs in a value"), string.Join(" | ", messages));
        Assert.That(messages, Has.None.Contains("Unknown directive"), "x:Null exists - it is just in the wrong place");
    }

    [Test]
    public void AnAttributeDirectiveWrittenAsAValueSaysSo()
    {
        var auml = AumlCodegenHarness.WindowHeader + "Title=\"{x:Key Whatever}\"><Grid /></Window>";

        AumlCodegenHarness.Generate(auml, out var errors);

        var messages = errors.Select(e => e.GetMessage()).ToList();
        Assert.That(messages, Has.Some.Contains("written on the element"), string.Join(" | ", messages));
        Assert.That(messages, Has.None.Contains("Unknown directive"), "x:Key exists - it is just in the wrong place");
    }

    // The drift that started this: {x:Null} was implemented and nothing listed it.
    [Test]
    public void NullIsBothImplementedAndListed()
    {
        var code = AumlCodegenHarness.Generate(
            AumlCodegenHarness.WindowHeader + "Background=\"{x:Null}\"><Grid /></Window>", out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Contain("SetValue(\"Background\", null)"), "{x:Null} must emit an explicit null");
        Assert.That(AumlDirectives.Find(AumlDirectives.Null), Is.Not.Null, "and tooling must know it exists");
    }
}
