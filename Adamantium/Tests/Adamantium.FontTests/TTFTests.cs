using Adamantium.Fonts;
using NUnit.Framework;

namespace Adamantium.FontTests
{
    public class TTFTests
    {
        [Test]
        public void LoadTTFFont()
        {
            var typeFace = Typeface.LoadFont(@"TTFFonts\SourceSans3-Regular.ttf", 3);
            
        }
        
        [Test]
        public void LoadTTFFont_SarabunRegular()
        {
            var typeFace = Typeface.LoadFont(@"TTFFonts\Sarabun-Regular.ttf", 3);
            var font = typeFace.GetFont(0);
            var glyph = font.GetGlyphByCharacter('@');
            //glyph.Triangulate(7);
        }
        
        [Test]
        public void LoadTTFFont_PlayfairDisplay()
        {
            var typeFace = Typeface.LoadFont(@"TTFFonts\PlayfairDisplay-Regular.ttf", 3);
            
        }
        
        [Test]
        public void LoadSystemFont()
        {
            var typeFace = Typeface.LoadSystemFont("arial", 3);
            
        }
    }
}