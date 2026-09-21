using System.Linq;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// A setter writes the SAME reference into every element it matches, so nothing that must be personal could live in a
/// theme: a <c>ContextMenu</c> has one <c>PlacementTarget</c>, a <c>Popup</c> one placement, a <c>Transform</c> one
/// owner. <c>x:Shared="False"</c> is how a value says "build me per target"; the generator hands the setter a factory
/// instead of an instance, and <see cref="Setter.Apply"/> calls it once per element.
/// </summary>
[TestFixture]
public class AumlSharedValueTests
{
    private const string StyleSetHeader =
        "<StyleSet x:Namespace=\"Test.App\" xmlns=\"http://adamantium/ui\" xmlns:x=\"http://adamantium/ui/xaml/extensions\">";

    private static string StyleWith(string value) =>
        StyleSetHeader +
        "<Style Selector=\"Button\"><Setter Property=\"ContextMenu\"><Setter.Value>" + value +
        "</Setter.Value></Setter></Style></StyleSet>";

    [Test]
    public void AValueWithoutTheDirectiveStaysOneSharedInstance()
    {
        var code = AumlCodegenHarness.Generate(StyleWith("<ContextMenu/>"), out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Not.Contain("PerTargetValue"), "sharing is the default and must stay it");
    }

    [Test]
    public void SharedFalseEmitsAFactoryInsteadOfAnInstance()
    {
        var code = AumlCodegenHarness.Generate(StyleWith("<ContextMenu x:Shared=\"False\"/>"), out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Contain("PerTargetValue"), "the setter has to be handed a factory");
        Assert.That(code, Does.Contain("object Build_shared"), "and the factory has to build the value");
    }

    [Test]
    public void TheGeneratedFactoryCompiles()
    {
        var errors = AumlCodegenHarness.Compile(StyleWith("<ContextMenu x:Shared=\"False\"/>"));

        Assert.That(errors, Is.Empty, "generated code did not compile: " + string.Join(" | ", errors.Select(d => d.ToString())));
    }

    // ---- the same directive on a resource DICTIONARY entry ----

    private const string DictionaryHeader =
        "<ResourceDictionary x:Namespace=\"Test.App\" xmlns=\"http://adamantium/ui\" xmlns:x=\"http://adamantium/ui/xaml/extensions\">";

    [Test]
    public void AKeyedEntryWithoutTheDirectiveIsStoredAsTheObject()
    {
        var code = AumlCodegenHarness.Generate(
            DictionaryHeader + "<ContextMenu x:Key=\"Menu\"/></ResourceDictionary>", out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Not.Contain("PerTargetValue"), "one dictionary entry, one object - that stays the default");
    }

    [Test]
    public void ASharedFalseEntryIsStoredAsAFactory()
    {
        var code = AumlCodegenHarness.Generate(
            DictionaryHeader + "<ContextMenu x:Key=\"Menu\" x:Shared=\"False\"/></ResourceDictionary>", out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Contain("object Build_shared"), "the entry has to be built by a factory");
        Assert.That(code, Does.Contain("PerTargetValue"), "and the dictionary has to store that factory");
        Assert.That(code, Does.Contain("Add(\"Menu\""), "under its key, like any other entry");
    }

    [Test]
    public void TheGeneratedDictionaryEntryCompiles()
    {
        var errors = AumlCodegenHarness.Compile(
            DictionaryHeader + "<ContextMenu x:Key=\"Menu\" x:Shared=\"False\"/></ResourceDictionary>");

        Assert.That(errors, Is.Empty, "generated code did not compile: " + string.Join(" | ", errors.Select(d => d.ToString())));
    }

    // The point of the whole thing: two calls, two objects. A shared value would hand back the same reference.
    [Test]
    public void TheFactoryBuildsAFreshValueEachTime()
    {
        var built = 0;
        var value = new PerTargetValue(() => { built++; return new object(); });

        var first = value.Create();
        var second = value.Create();

        Assert.That(built, Is.EqualTo(2), "each target must cost its own build");
        Assert.That(first, Is.Not.SameAs(second));
    }
}
