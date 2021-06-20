using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Parsers;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts
{
    internal class Font : IFont
    {
        private List<Glyph> glyphs;
        private List<UInt32> unicodes;
        private List<FontLanguage> languages;
        private Dictionary<string, FontLanguage> languagesMap;

        private Dictionary<string, Glyph> nameToGlyph;
        private Dictionary<UInt32, Glyph> unicodeToGlyph;
        
        internal TypeFace TypeFace { get; }
        internal VariationStore VariationData { get; set; }
        internal List<InstanceRecord> InstanceData { get; set; }

        public Font(TypeFace typeFace)
        {
            TypeFace = typeFace;
            glyphs = new List<Glyph>();
            unicodes = new List<uint>();
            languages = new List<FontLanguage>();

            nameToGlyph = new Dictionary<string, Glyph>();
            unicodeToGlyph = new Dictionary<uint, Glyph>();
            
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
        }
        
        public bool isGlyphNamesProvided { get; internal set; }

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
        
        public ushort UnitsPerEm { get; internal set; }
        
        /// <summary>
        /// smallest readable size in pixels
        /// </summary>
        public UInt16 LowestRecPPEM { get; internal set; } 
        
        /// <summary>
        /// space between lines
        /// </summary>
        public Int32 LineSpace { get; internal set; }
        
        public DateTime Created { get; internal set; }
        
        public DateTime Modified { get; internal set; }

        public IReadOnlyCollection<Glyph> Glyphs => glyphs.AsReadOnly();
        
        public IReadOnlyCollection<uint> Unicodes => unicodes.AsReadOnly();

        public IReadOnlyCollection<FontLanguage> Languages => languages.AsReadOnly();

        internal KerningSubtable[] KerningData { get; set; }
        
        internal void SetGlyphs(IEnumerable<Glyph> inputGlyphs)
        {
            glyphs.Clear();
            glyphs.AddRange(inputGlyphs);
        }

        internal void SetLanguages(IEnumerable<FontLanguage> inputLanguages)
        {
            languages.Clear();
            languages.AddRange(inputLanguages);
            languagesMap = languages.ToDictionary(x => x.ShortName);
        }

        public bool IsLanguageAvailable(string language)
        {
            return languagesMap.ContainsKey(language);
        }

        public void AddLanguage(FontLanguage language)
        {
            if (!IsLanguageAvailable(language.ShortName))
            {
                languages.Add(language);
                languagesMap[language.ShortName] = language;
            }
        }

        void IFont.UpdateGlyphNamesCache()
        {
            if (!isGlyphNamesProvided) return;
            
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
            
            foreach (var (key, value) in glyphMapping)
            {
                unicodes.AddRange(value);
                foreach (var unicode in value)
                {
                    if (TypeFace.GetGlyphByIndex(key, out var glyph))
                    {
                        unicodeToGlyph[unicode] = glyph;
                    }
                }
            }
        }

        public Glyph GetGlyphByIndex(uint index)
        {
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
                return null;
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