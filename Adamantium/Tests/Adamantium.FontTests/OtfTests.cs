using System.IO;
using Adamantium.Fonts;
using Adamantium.Fonts.OTF;
using NUnit.Framework;

namespace Adamantium.FontTests
{
    public class OtfTests
    {
        [Test]
        public void LoadOtfCffFont()
        {
            var typeFace = TypeFace.LoadFont(Path.Combine("OTFFonts", "FontCollections", "ASANA.TTC"), 2);
            //var typeFace = TypeFace.LoadFont(Path.Combine("OTFFonts", "FontCollections", "NotoSansCJK-Regular.ttc"), 2);
            //var fp = new OTFParser(@"OTFFonts\Glametrix-oj9A.otf");
            //var fp = new OTFParser(@"OTFFonts\FDArrayTest257.otf");
            //var fp = new OTFParser(@"OTFFonts\Poppins-Medium.otf");
            //var fp = new OTFParser(@"OTFFonts\customfont.otf");
        }
        
        [Test]
        public void LoadOtfCff2Font()
        {
            //var fp = new OTFParser(@"OTFFonts\CFF2]\AdobeVFPrototype.otf");
            //var fp = new OTFParser(@"OTFFonts\CFF2\SourceSans3-Regular.otf");
            //var fp = new OTFParser(@"OTFFonts\customfont.otf");
        }
    }
}