using System;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// A flags enum written in markup, in both spellings. The COMMA is what .NET's own parser takes; the PIPE is what an
/// author reaches for, because it is how the same value is written in C#.
/// <para>Both have to be understood by the compiler AND by the loader. Accepted by one and rejected by the other is the
/// worse failure of the two: the markup compiles, ships, and then throws from inside the loader on a machine that reads
/// the file at runtime instead of generating code from it.</para>
/// </summary>
[TestFixture]
public class AumlEnumFlagsTests
{
    private const string Header =
        AumlCodegenHarness.WindowHeader + "xmlns:controls=\"clr-namespace:Adamantium.UI.Controls;assembly=Adamantium.UI.Controls\">";

    /// <summary>The pipe, through the real generator: it must become a C# OR of the two members, not the text as
    /// written - "PagerDisplayMode.FirstLast|Numeric" is a syntax error inside generated code, which reaches the author
    /// as a compiler error in a file they never wrote.</summary>
    [Test]
    public void ThePipeSpelling_GeneratesAnOrOfBothMembers()
    {
        var code = AumlCodegenHarness.Generate(
            Header + "<DataPager DisplayMode=\"FirstLast|Numeric\"/></Window>", out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Contain("PagerDisplayMode.FirstLast | ").And.Contain("PagerDisplayMode.Numeric"),
            "the two members have to be OR-ed\n" + code);
    }

    /// <summary>The comma keeps working - this is an addition, not a swap.</summary>
    [Test]
    public void TheCommaSpelling_StillGeneratesAnOrOfBothMembers()
    {
        var code = AumlCodegenHarness.Generate(
            Header + "<DataPager DisplayMode=\"FirstLast,Numeric\"/></Window>", out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Contain("PagerDisplayMode.FirstLast | ").And.Contain("PagerDisplayMode.Numeric"),
            code);
    }

    /// <summary>A single member is untouched by any of this.</summary>
    [Test]
    public void ASingleMember_IsEmittedAsItself()
    {
        var code = AumlCodegenHarness.Generate(
            Header + "<DataPager DisplayMode=\"Numeric\"/></Window>", out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
        Assert.That(code, Does.Contain("PagerDisplayMode.Numeric"), code);
    }

    // ---- and the same two spellings on the LOADER's side -----------------------------------------------------------

    [TestCase("FirstLast|Numeric")]
    [TestCase("FirstLast,Numeric")]
    [TestCase("FirstLast, Numeric")]
    [TestCase("firstlast|numeric")]
    public void BothSpellings_ParseToTheSameSet(string text)
    {
        var value = (PagerDisplayMode)TypeCastFactory.ParseEnum(typeof(PagerDisplayMode), text);

        Assert.That(value, Is.EqualTo(PagerDisplayMode.FirstLast | PagerDisplayMode.Numeric));
    }

    /// <summary>...and it is the SAME entry point the general conversion uses, so a value set through a style setter or
    /// a trigger cannot understand one spelling while the loader understands the other.</summary>
    [Test]
    public void TheGeneralConversion_TakesThePipeToo()
    {
        var value = TypeCastFactory.CastFromString("FirstLast|Numeric", typeof(PagerDisplayMode));

        Assert.That(value, Is.EqualTo(PagerDisplayMode.FirstLast | PagerDisplayMode.Numeric));
    }

    /// <summary>A name that is not a member is still an error - the point is to accept another separator, not to start
    /// swallowing typos.</summary>
    [Test]
    public void ANameThatIsNotAMember_IsStillRefused()
    {
        Assert.Throws<ArgumentException>(
            () => TypeCastFactory.ParseEnum(typeof(PagerDisplayMode), "FirstLast|Nonsense"));

        Assert.That(TypeCastFactory.TryParseEnum(typeof(PagerDisplayMode), "Nonsense", out _), Is.False);
    }
}
