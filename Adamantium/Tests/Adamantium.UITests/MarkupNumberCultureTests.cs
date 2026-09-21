using System.Globalization;
using System.Threading;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Markup is a machine format: "0.5" means one half on every machine that reads it. Converting it under the reader's
/// locale made every fractional value in every theme silently unparseable wherever the decimal separator is a comma -
/// the value became UnsetValue, the property kept its default, and the markup did nothing with nothing to see.
/// </summary>
[TestFixture]
public class MarkupNumberCultureTests
{
    private static object CastUnder(string culture, string text, System.Type type)
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
            return TypeCastFactory.CastFromString(text, type);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [TestCase("ru-RU")]
    [TestCase("de-DE")]
    [TestCase("fr-FR")]
    [TestCase("en-US")]
    [TestCase("")]
    public void AFractionalMarkupValueMeansTheSameOnEveryMachine(string culture)
    {
        Assert.Multiple(() =>
        {
            Assert.That(CastUnder(culture, "0.5", typeof(double)), Is.EqualTo(0.5).Within(0.0001));
            Assert.That(CastUnder(culture, "1.2", typeof(double)), Is.EqualTo(1.2).Within(0.0001));
            Assert.That(CastUnder(culture, "0.28", typeof(float)), Is.EqualTo(0.28f).Within(0.0001f));
        });
    }
}
