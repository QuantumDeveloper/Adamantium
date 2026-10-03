using Adamantium.Fonts.Common;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Parsers.CFF;
using Adamantium.Fonts.Tables.CFF;
using NUnit.Framework;

namespace Adamantium.FontTests;

public class CffTableTests
{
    [Test]
    public void FontDictOfAGlyphIsTheLastRangeStartingAtOrBeforeIt()
    {
        var info = new CIDFontInfo
        {
            FdSelectFormat = 3,
            FdRanges = [new FDRange(0, 1), new FDRange(10, 2), new FDRange(10, 3), new FDRange(20, 4), new FDRange(30, 0)]
        };
        var selector = new FontDictArraySelector(info);

        Assert.That(selector.SelectFontDictArray(0), Is.EqualTo(1));
        Assert.That(selector.SelectFontDictArray(9), Is.EqualTo(1));
        Assert.That(selector.SelectFontDictArray(10), Is.EqualTo(3));
        Assert.That(selector.SelectFontDictArray(29), Is.EqualTo(4));
        Assert.That(selector.SelectFontDictArray(5), Is.EqualTo(1));
    }

    [Test]
    public void FontDictSelectFormat4ReadsWideRanges()
    {
        byte[] data =
        [
            4,
            0, 0, 0, 2,
            0, 0, 0, 0, 0, 1,
            0, 1, 0, 0, 1, 44,
            0, 1, 0, 10
        ];
        var font = new CFFFont(new CFFFontSet(), CFFVersion.CFF2);

        new FontStreamReader(data).ReadFDSelect(font, 65546);
        var selector = new FontDictArraySelector(font.CIDFontInfo);

        Assert.That(selector.SelectFontDictArray(7), Is.EqualTo(1));
        Assert.That(selector.SelectFontDictArray(65540), Is.EqualTo(300));
    }

    [Test]
    public void DeltaArrayIsDecodedAsARunningSum()
    {
        byte[] blueValues = [127, 151, 248, 122, 151, 6];
        double[] decoded = [-12, 0, 486, 498];

        var dict = new PrivateDictParser(blueValues, new CFFFont(new CFFFontSet(), CFFVersion.CFF));

        Assert.That(dict[DictOperatorsType.BlueValues].AsList(), Is.EqualTo(decoded));
    }

    [Test]
    public void BlendAppliesEachDeltaToItsOwnRegion()
    {
        var regions = new VariationRegionList
        {
            AxisCount = 1,
            RegionCount = 3,
            VariationRegions = [Region(0, 1, 1), Region(-1, -1, 0), Region(0, 0.5f, 1)]
        };
        var plain = new CommandOperand(7);
        var blended = new CommandOperand(100) { BlendData = new RegionData { RegionIndices = [2, 0] } };
        blended.BlendData.Data.AddRange([10, 20]);
        var command = new Command { Operator = OperatorsType.rlineto, Operands = [plain, blended], IsBlendPresent = true };

        command.ApplyBlend(regions, [0.5f]);

        Assert.That(blended.Value, Is.EqualTo(120));
        Assert.That(plain.Value, Is.EqualTo(7));
    }

    private static VariationRegion Region(float start, float peak, float end)
    {
        return new VariationRegion
        {
            RegionAxes = [new RegionAxisCoordinates { StartCoord = start, PeakCoord = peak, EndCoord = end }]
        };
    }
}
