using Adamantium.Fonts.OTF;
using NUnit.Framework;

namespace Adamantium.FontTests
{
    public class OtfTests
    {
        [Test]
        public void LoadOTFFont()
        {
            var fp = new OTFParser(@"OTFFonts\Glametrix-oj9A.otf");
            //var fp = new OTFParser(@"OTFFonts\Poppins-Medium.otf");
            //var fp = new OTFParser(@"OTFFonts\customfont.otf");
        }
    }
}