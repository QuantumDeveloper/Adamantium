using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Parsers;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts
{
    public class Font : IFont
    {
        private List<Glyph> glyphs;
        private List<UInt32> unicodes;
        private Dictionary<string, Glyph> nameToGlyph;
        private Dictionary<UInt32, Glyph> unicodeToGlyph;
        private Dictionary<string, List<Feature>> featuresMap;
        internal Typeface Typeface { get; }
        internal VariationStore VariationData { get; set; }
        internal List<InstanceRecord> InstanceData { get; set; }

        public Font(Typeface typeface)
        {
            Typeface = typeface;
            glyphs = new List<Glyph>();
            unicodes = new List<uint>();

            nameToGlyph = new Dictionary<string, Glyph>();
            unicodeToGlyph = new Dictionary<uint, Glyph>();

            featuresMap = new Dictionary<string, List<Feature>>();

            Copyright = String.Empty;
            FontFamily = String.Empty;
            FontSubfamily = String.Empty;
            UniqueId = String.Empty;
            FullName = String.Empty;
            Version = String.Empty;
            Trademark = String.Empty;
            Manufacturer = String.Empty;
            Designer = String.Empty;
            Description = String.Empty;
            VendorUrl = String.Empty;
            DesignerUrl = String.Empty;
            LicenseDescription = String.Empty;
            LicenseInfoUrl = String.Empty;
            TypographicFamilyName = String.Empty;
            TypographicSubfamilyName = String.Empty;
            WwsFamilyName = String.Empty;
            WwsSubfamilyName = String.Empty;
            LightBackgroundPalette = String.Empty;
            DarkBackgroundPalette = String.Empty;

            NotDefLayoutData = new GlyphLayoutData(0);

            FeatureService = new FeatureService();
        }

        public bool IsGlyphNamesProvided { get; internal set; }

        // Name info section ---
        public string Copyright { get; internal set; }
        public string FontFamily { get; internal set; }
        public string FontSubfamily { get; internal set; }
        public string UniqueId { get; internal set; }
        public string FullName { get; internal set; }
        public string Version { get; internal set; }
        public string Trademark { get; internal set; }
        public string Manufacturer { get; internal set; }
        public string Designer { get; internal set; }
        public string Description { get; internal set; }
        public string VendorUrl { get; internal set; }
        public string DesignerUrl { get; internal set; }
        public string LicenseDescription { get; internal set; }
        public string LicenseInfoUrl { get; internal set; }
        public string TypographicFamilyName { get; internal set; }
        public string TypographicSubfamilyName { get; internal set; }
        public string WwsFamilyName { get; internal set; }
        public string WwsSubfamilyName { get; internal set; }
        public string LightBackgroundPalette { get; internal set; }
        public string DarkBackgroundPalette { get; internal set; }

        // ------
        public FeatureService FeatureService { get; }
        public uint GlyphCount => (uint)glyphs.Count;
        public ushort UnitsPerEm { get; internal set; }
        public Int16 Ascender { get; internal set; }
        public Int16 Descender { get; internal set; }
        public Int16 CapsHeight { get; internal set; }
        
        public short LineGap { get; internal set; }
        
        public Int16 Baseline { get; internal set; }

        /// <summary>
        /// smallest readable size in pixels
        /// </summary>
        public UInt16 LowestRecPPEM { get; internal set; }

        /// <summary>
        /// space between lines
        /// </summary>
        public Double LineSpacingMultiplier { get; internal set; }

        public DateTime Created { get; internal set; }

        public DateTime Modified { get; internal set; }

        public IReadOnlyCollection<Glyph> Glyphs => glyphs.AsReadOnly();
        public IReadOnlyCollection<uint> Unicodes => unicodes.AsReadOnly();
        public GlyphLayoutData NotDefLayoutData { get; }
        internal KerningSubtable[] KerningData { get; set; }

        internal void SetGlyphs(IEnumerable<Glyph> inputGlyphs)
        {
            glyphs.Clear();
            glyphs.AddRange(inputGlyphs);
        }

        void IFont.UpdateGlyphNamesCache()
        {
            if (!IsGlyphNamesProvided) return;

            foreach (var glyph in glyphs)
            {
                var name = glyph.Name;
                if (nameToGlyph.ContainsKey(glyph.Name))
                {
                    name = GetUniqueName(glyph.Name);
                }

                nameToGlyph[name] = glyph;
            }
        }

        private string GetUniqueName(string originalName)
        {
            int count = 1;

            string uniqueName = originalName;
            while (nameToGlyph.ContainsKey(uniqueName))
            {
                uniqueName = $"{originalName}.{count++}";
            }

            return uniqueName;
        }

        void IFont.SetGlyphUnicodes(Dictionary<uint, List<uint>> glyphMapping)
        {
            unicodes.Clear();
            unicodeToGlyph.Clear();

            foreach (var kvp in glyphMapping)
            {
                unicodes.AddRange(kvp.Value);
                foreach (var unicode in kvp.Value)
                {
                    if (Typeface.GetGlyphByIndex(kvp.Key, out var glyph))
                    {
                        unicodeToGlyph[unicode] = glyph;
                    }
                }
            }
        }
        
        public IReadOnlyList<Glyph> TranslateIntoGlyphs(string input)
        {
            var translatedGlyphs = new List<Glyph>();
            foreach (var character in input)
            {
                var glyph = GetGlyphByCharacter(character);
                if (glyph == null)
                {
                    Typeface.GetGlyphByIndex(0U, out glyph);
                }
                // ONCE per character, not once per translation. The glyph comes out of the font's own map - it is SHARED
                // and lives as long as the font - so this list did too: every measure of every string appended another
                // copy of the same character to it, for the lifetime of the process. Measuring "Hello" a million times
                // left a million 'l's on one glyph. That is both the heap that never comes back and the ~117KB a single
                // text measure allocated (the list re-doubling its backing array, the old one becoming garbage).
                // Every consumer reads RelatedCharacters.FirstOrDefault() - one representative character for the glyph's
                // texture - so a character already recorded adds nothing at all.
                if (!glyph.RelatedCharacters.Contains(character))
                {
                    // Locked only on the rare first sight of a character: text is laid out from the layout thread AND
                    // from parallel arrange, and an unguarded Add on a shared List is a torn list, not a wrong number.
                    lock (glyph.RelatedCharacters)
                    {
                        if (!glyph.RelatedCharacters.Contains(character)) glyph.RelatedCharacters.Add(character);
                    }
                }

                translatedGlyphs.Add(glyph);
            }

            return translatedGlyphs;
        }

        public Glyph GetGlyphByIndex(uint index)
        {
            if (index >= glyphs.Count)
            {
                Typeface.GetGlyphByIndex(0, out var glyph);
                return glyph;
            }
            
            return glyphs[(int)index];
        }

        public Glyph GetGlyphByName(string name)
        {
            if (!nameToGlyph.TryGetValue(name, out var glyph))
            {
                return null;
            }

            return glyph;
        }

        public Glyph GetGlyphByUnicode(uint unicode)
        {
            if (!unicodeToGlyph.TryGetValue(unicode, out var glyph))
            {
                return glyphs[0];
            }

            return glyph;
        }

        public Glyph GetGlyphByCharacter(char character)
        {
            return GetGlyphByUnicode(character);
        }

        public Int16 GetKerningValue(UInt16 leftGlyphIndex, UInt16 rightGlyphIndex)
        {
            if (KerningData == null)
            {
                return 0;
            }

            Int16 kerningValue = 0;

            UInt32 key = TTFParser.GenerateKerningKey(leftGlyphIndex, rightGlyphIndex);

            foreach (var data in KerningData)
            {
                if (!data.KerningValues.ContainsKey(key)) continue;

                kerningValue = data.KerningValues[key];
                break;
            }

            return kerningValue;
        }
    }
}