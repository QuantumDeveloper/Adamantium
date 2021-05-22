using System;
using Adamantium.Fonts;
using Adamantium.Fonts.DataOut;
using NUnit.Framework;

namespace Adamantium.FontTests
{
    public class TTFTests
    {
        [Test]
        public void LoadTTFFont()
        {
            var typeFace = TypeFace.LoadFont(@"TTFFonts\SourceSans3-Regular.ttf", 3);
            
        }
        
        [Test]
        public void LoadTTFFont_SarabunRegular()
        {
            var typeFace = TypeFace.LoadFont(@"TTFFonts\Sarabun-Regular.ttf", 3);
            var font = typeFace.GetFont(0);
            var glyph = font.GetGlyphByCharacter('@');
            glyph.Triangulate(7);
        }
        
        [Test]
        public void LoadTTFFont_PlayfairDisplay()
        {
            var typeFace = TypeFace.LoadFont(@"TTFFonts\PlayfairDisplay-Regular.ttf", 3);
            
        }
    }
}