using System.IO;
using Adamantium.Fonts;
using NUnit.Framework;

namespace Adamantium.FontTests
{
    public class Woff2Tests
    {
        internal static class WoffFonts
        {
            public static string Sarabun_Regular;
        }

        static Woff2Tests()
        {
            WoffFonts.Sarabun_Regular = Path.Combine("WoffFonts", "Sarabun-Regular.woff2");
        }
        
        [Test]
        public void LoadWoff2Font()
        {
            var typeFace = Typeface.LoadFont(WoffFonts.Sarabun_Regular, 3);
        }
        
        [Test]
        public void LoadWoff2FontCollectionAsana()
        {
            var typeFace = Typeface.LoadFont(Path.Combine("WoffFonts", "Woff2Collection", "ASANA.woff2"), 3);
            
        }
        
        [Test]
        public void LoadWoff2FontCollectionNotoSans()
        {
            var typeFace = Typeface.LoadFont(Path.Combine("WoffFonts", "Woff2Collection", "NotoSansCJK-Regular.woff2"), 3);
            
        }
    }
}