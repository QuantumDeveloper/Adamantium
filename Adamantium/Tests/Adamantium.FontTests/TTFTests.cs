using Adamantium.Fonts.DataOut;
using Adamantium.Fonts.OTF;
using Adamantium.Fonts.TTF;
using NUnit.Framework;

namespace Adamantium.FontTests
{
    public class TTFTests
    {
        [Test]
        public void LoadTTFFont()
        {
            //TTFFontParser fp = new TTFFontParser(@"D:\Test.ttf", 7);
            TTFParser parser = new TTFParser(@"PlayfairDisplay-Regular.ttf", 7);

            char ch = 'i';

            var data = parser.FontData.GetGlyphForCharacter(ch);
        }
        
        [Test]
        public void TTFTriangulationTimeTest()
        {
            //TTFFontParser fp = new TTFFontParser(@"D:\Test.ttf", 7);
            TTFParser parser = new TTFParser(@"PlayfairDisplay-Regular.ttf", 7);

            foreach (var glyphData in parser.FontData.GlyphData)
            {
                parser.GenerateGlyphTriangles(glyphData);
            }
        }
        
        [Test]
        public void TTFTriangulation2TimeTest()
        {
            //TTFFontParser fp = new TTFFontParser(@"D:\Test.ttf", 7);
            TTFParser parser = new TTFParser(@"PlayfairDisplay-Regular.ttf", 7);

            foreach (var glyphData in parser.FontData.GlyphData)
            {
                parser.GenerateGlyphTriangles(glyphData);
            }
        }
    }
}