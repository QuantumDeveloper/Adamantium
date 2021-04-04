using System;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.OTF
{
    internal class Font : IFont
    {
        private List<Glyph> glyphs;
        private List<UInt32> unicodes;

        public Font()
        {
            glyphs = new List<Glyph>();
            unicodes = new List<uint>();

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
        
        public IReadOnlyCollection<uint> Unicodes => unicodes.AsReadOnly();
        public IReadOnlyCollection<Glyph> Glyphs => glyphs.AsReadOnly();
        
        public Glyph GetGlyphByName(string name)
        {
            throw new NotImplementedException();
        }

        public Glyph GetGlyphByUnicode(uint unicode)
        {
            throw new NotImplementedException();
        }

        public Glyph GetGlyphByIndex(uint index)
        {
            throw new NotImplementedException();
        }
        
        internal void SetGlyphs(IEnumerable<Glyph> glyphs)
        {
            this.glyphs.Clear();
            this.glyphs.AddRange(glyphs);
        }
    }
}