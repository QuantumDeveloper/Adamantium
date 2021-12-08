using System.IO;
using Adamantium.Fonts;
using NUnit.Framework;

namespace Adamantium.FontTests
{
    public class WoffTests
    {
        internal static class WoffFonts
        {
            public static string Sarabun_Regular;
        }

        static WoffTests()
        {
            WoffFonts.Sarabun_Regular = Path.Combine("WoffFonts", "Sarabun-Regular.woff");
        }
        
        [Test]
        public void LoadWoffFont()
        {
            var typeFace = TypeFace.LoadFont(WoffFonts.Sarabun_Regular, 3);
        }
    }
}