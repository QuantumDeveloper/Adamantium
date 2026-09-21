using Adamantium.Core.Collections;
using Adamantium.Core.TypeParsing;
using NUnit.Framework;

namespace Adamantium.CoreTests;

[TestFixture]
public class TypeParsingTests
{
    // The AUML codegen routes a collection set from a STRING attribute (e.g. StrokeDashArray="36,24") through
    // TypeParser.Parse<TrackingCollection<double>>; DoubleCollectionParser must turn that string into a populated
    // collection (the bug was an empty/null collection -> the dash array carried no data).
    [Test]
    public void DoubleCollection_ParsesCommaList()
    {
        var result = TypeParser.Parse<TrackingCollection<double>>("36,24");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0], Is.EqualTo(36.0));
        Assert.That(result[1], Is.EqualTo(24.0));
    }

    [Test]
    public void DoubleCollection_ParsesSpaceAndCommaSeparators()
    {
        var result = TypeParser.Parse<TrackingCollection<double>>("10 6, 2  6");

        Assert.That(result.Count, Is.EqualTo(4));
        Assert.That(result[0], Is.EqualTo(10.0));
        Assert.That(result[3], Is.EqualTo(6.0));
    }

    [Test]
    public void DoubleCollection_EmptyString_GivesEmptyCollection()
    {
        var result = TypeParser.Parse<TrackingCollection<double>>("");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(0));
    }
}
