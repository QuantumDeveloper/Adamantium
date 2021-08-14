using Adamantium.Fonts.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adamantium.Fonts
{
    internal class FontManager
    {
        private TypeFace currentTypeFace;
        private List<TypeFace> typeFaces;
        public IReadOnlyCollection<TypeFace> TypeFaces => typeFaces.AsReadOnly();

        public FontManager(TypeFace typeFace)
        {
            typeFaces = new List<TypeFace>();
            AddTypeFace(typeFace);
        }

        public void AddTypeFace(TypeFace typeFace)
        {
            typeFaces.Add(typeFace);
        }

        public static async Task<FontManager> LoadTypeFace(string font)
        {
            var typeFace = await TypeFace.LoadFontAsync(font, 3); // @TODO think and change the resolution approach
            return new FontManager(typeFace);
        }

        public void SetCurrentTypeFace(TypeFace typeFace)
        {
            currentTypeFace = typeFace;
        }

        public void SetCurrentTypeFace(IFont font)
        {
            currentTypeFace.SetCurrentFont(font);
        }

        public /*GlyphCompositeData*/ void GetGlyph(char character)
        {
            var glyph = currentTypeFace.CurrentFont.GetGlyphByCharacter(character);

            var systemLanguage = CultureInfo.CurrentCulture.ThreeLetterISOLanguageName;

            if (!currentTypeFace.CurrentFont.IsLanguageAvailableByIsoName(systemLanguage)) return; // @TODO return GlyphCompositeData for notDef glyph

            foreach (var feature in currentTypeFace.CurrentFont.EnabledFeatures)
            {
                if (!currentTypeFace.CurrentFont.IsCharacterCached(currentLanguage, feature, character);
            }
        }
    }
}
