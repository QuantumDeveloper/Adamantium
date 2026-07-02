using Adamantium.Core.TypeParsing;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Collections;
using Adamantium.UI.Core.TypeParsers;
using NUnit.Framework;

namespace Adamantium.UITests;

// Polygon/Polyline Points authored in markup ("x,y x,y …") reach TypeParser.Parse<PointsCollection> at runtime; without a
// registered parser that threw "Type parser not found for PointsCollection" (a Shapes-tab crash). The [TypeParser] attr
// on PointsCollection wires PointsCollectionParser, exercised here through the exact runtime path the codegen emits.
[TestFixture]
public class PointsCollectionParserTests
{
    [Test]
    public void Parses_CommaSeparatedPairs_ViaTypeParser()
    {
        var points = TypeParser.Parse<PointsCollection>("60,0 120,96 0,96");
        Assert.Multiple(() =>
        {
            Assert.That(points, Has.Count.EqualTo(3));
            Assert.That(points[0], Is.EqualTo(new Vector2(60, 0)));
            Assert.That(points[1], Is.EqualTo(new Vector2(120, 96)));
            Assert.That(points[2], Is.EqualTo(new Vector2(0, 96)));
        });
    }

    [Test]
    public void Parses_WhitespaceOnlySeparators()
    {
        var points = new PointsCollectionParser().Parse("10 20 30 40");
        Assert.Multiple(() =>
        {
            Assert.That(points, Has.Count.EqualTo(2));
            Assert.That(points[0], Is.EqualTo(new Vector2(10, 20)));
            Assert.That(points[1], Is.EqualTo(new Vector2(30, 40)));
        });
    }
}
