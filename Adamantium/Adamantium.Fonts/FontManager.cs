using Adamantium.Fonts.Common;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Adamantium.Fonts
{
    internal class FontManager
    {
        private TypeFace currentTypeFace;
        private readonly List<TypeFace> typeFaces;
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

        public static async Task<FontManager> LoadTypeFaceAsync(string font)
        {
            var typeFace = await TypeFace.LoadFontAsync(font, 3); // @TODO think and change the resolution approach
            return new FontManager(typeFace);
        }

        public void SetCurrentTypeFace(TypeFace typeFace)
        {
            currentTypeFace = typeFace;
        }

        public void SetCurrentFont(IFont font)
        {
            currentTypeFace.SetCurrentFont(font);
        }

        public bool TryGetCurrentLanguage(out FontLanguage lang)
        {
            var systemLanguage = CultureInfo.CurrentCulture.ThreeLetterISOLanguageName;
            lang = null;

            if (!currentTypeFace.CurrentFont.IsLanguageAvailableByIsoName(systemLanguage, out var currentLanguage))
            {
                return false;
            }

            return true;
        }

        public bool ProcessGlyphLayout(char character)
        {
            var glyph = currentTypeFace.CurrentFont.GetGlyphByCharacter(character);

            if (!TryGetCurrentLanguage(out var currentLanguage)) return false;

            foreach (var feature in currentTypeFace.CurrentFont.FeatureManager.EnabledFeatures)
            {
                if (!currentTypeFace.CurrentFont.IsFeatureCached(currentLanguage, glyph, feature.Info))
                {
                    feature.Apply(currentLanguage, currentTypeFace, glyph);
                }
            }

            return true;
        }
        
        public GlyphLayoutData GetGlyphLayoutData(FeatureInfo featureInfo, char character)
        {
            if (TryGetCurrentLanguage(out var currentLanguage))
            {
                return currentTypeFace.CurrentFont.GetGlyphLayoutData(currentLanguage, featureInfo, character);
            }

            return currentTypeFace.CurrentFont.NotDefLayoutData;
        }
        
    }
}
