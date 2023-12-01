using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Parsers;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.CFF;
using MessagePack;

namespace Adamantium.Fonts
{
    [MessagePackObject]
    public class Font : IFont
    {
        [Key(5)]
        private List<Glyph> glyphs;
        [Key(1)]
        private List<UInt32> unicodes;
        [Key(2)]
        private Dictionary<string, Glyph> nameToGlyph;
        [Key(3)]
        private Dictionary<UInt32, Glyph> unicodeToGlyph;
        [Key(4)]
        private Dictionary<string, List<Feature>> featuresMap;
        [IgnoreMember]
        internal TypeFace TypeFace { get; }
        [Key(6)]
        internal VariationStore VariationData { get; set; }
        [Key(7)]
        internal List<InstanceRecord> InstanceData { get; set; }

        public Font(TypeFace typeFace)
        {
            TypeFace = typeFace;
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

        [Key(8)]
        public bool IsGlyphNamesProvided { get; internal set; }

        // Name info section ---
        [Key(9)]
        public string Copyright { get; internal set; }
        [Key(10)]
        public string FontFamily { get; internal set; }
        [Key(11)]
        public string FontSubfamily { get; internal set; }
        [Key(12)]
        public string UniqueId { get; internal set; }
        [Key(13)]
        public string FullName { get; internal set; }
        [Key(14)]
        public string Version { get; internal set; }
        [Key(15)]
        public string Trademark { get; internal set; }
        [Key(16)]
        public string Manufacturer { get; internal set; }
        [Key(17)]
        public string Designer { get; internal set; }
        [Key(18)]
        public string Description { get; internal set; }
        [Key(19)]
        public string VendorUrl { get; internal set; }
        [Key(20)]
        public string DesignerUrl { get; internal set; }
        [Key(21)]
        public string LicenseDescription { get; internal set; }
        [Key(22)]
        public string LicenseInfoUrl { get; internal set; }
        [Key(23)]
        public string TypographicFamilyName { get; internal set; }
        [Key(24)]
        public string TypographicSubfamilyName { get; internal set; }
        [Key(25)]
        public string WwsFamilyName { get; internal set; }
        [Key(26)]
        public string WwsSubfamilyName { get; internal set; }
        [Key(27)]
        public string LightBackgroundPalette { get; internal set; }
        [Key(28)]
        public string DarkBackgroundPalette { get; internal set; }

        // ------
        [IgnoreMember]
        public FeatureService FeatureService { get; }
        [IgnoreMember]
        public uint GlyphCount => (uint)glyphs.Count;
        [Key(29)]
        public ushort UnitsPerEm { get; internal set; }
        [Key(30)]
        public Int16 Ascender { get; internal set; }

        /// <summary>
        /// smallest readable size in pixels
        /// </summary>
        [Key(31)]
        public UInt16 LowestRecPPEM { get; internal set; }

        /// <summary>
        /// space between lines
        /// </summary>
        [Key(32)]
        public Int32 LineSpace { get; internal set; }

        [Key(33)]
        public DateTime Created { get; internal set; }

        [Key(34)]
        public DateTime Modified { get; internal set; }

        [IgnoreMember]
        public IReadOnlyCollection<Glyph> Glyphs => glyphs.AsReadOnly();
        [IgnoreMember]
        public IReadOnlyCollection<uint> Unicodes => unicodes.AsReadOnly();
        [IgnoreMember]
        public GlyphLayoutData NotDefLayoutData { get; }
        [Key(35)]
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
                    if (TypeFace.GetGlyphByIndex(kvp.Key, out var glyph))
                    {
                        unicodeToGlyph[unicode] = glyph;
                    }
                }
            }
        }
        
        public Glyph[] TranslateIntoGlyphs(string input)
        {
            var translatedGlyphs = new List<Glyph>();
            foreach (var character in input)
            {
                var glyph = GetGlyphByCharacter(character);
                translatedGlyphs.Add(glyph);
            }

            return translatedGlyphs.ToArray();
        }

        public Glyph GetGlyphByIndex(uint index)
        {
            if (index >= glyphs.Count)
            {
                TypeFace.GetGlyphByIndex(0, out var glyph);
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