using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;

namespace Adamantium.Fonts
{
    [MessagePack.Union(0, typeof(Font))]
    public interface IFont
    {
        #region Name
        
        string Copyright { get; }                
        string FontFamily { get; }              
        string FontSubfamily { get; }            
        string UniqueId { get; }
        string FullName { get; }
        string Version { get; }           
        string Trademark { get; }                
        string Manufacturer { get; }             
        string Designer { get; }                 
        string Description { get; }             
        string VendorUrl { get; }
        string DesignerUrl { get; }
        string LicenseDescription { get; }      
        string LicenseInfoUrl { get; }          
        string TypographicFamilyName { get; }   
        string TypographicSubfamilyName { get; }
        
        /// <summary>
        /// // WWS - weight, width, slope
        /// </summary>
        string WwsFamilyName { get; }
        
        /// <summary>
        /// // WWS - weight, width, slope
        /// </summary>
        string WwsSubfamilyName { get; }        
        string LightBackgroundPalette { get; }  
        string DarkBackgroundPalette { get; }
        
        #endregion
        
        public FeatureService FeatureService { get; }
        
        public GlyphLayoutData NotDefLayoutData { get; }
        
        public IReadOnlyCollection<uint> Unicodes { get; }
        
        public uint GlyphCount { get; }
        
        public ushort UnitsPerEm { get; }
        
        public Int16 Ascender { get; } 
        
        public Int16 Descender { get; }
        
        public Int16 CapsHeight { get; }
        
        public Int16 LineGap { get; }
        
        public Int16 Baseline { get; }
        /// <summary>
        /// smallest readable size in pixels
        /// </summary>
        public UInt16 LowestRecPPEM { get; }

        /// <summary>
        /// space between lines
        /// </summary>
        public Double LineSpacingMultiplier { get; }

        public DateTime Created { get; }

        public DateTime Modified { get; }

        public IReadOnlyCollection<Glyph> Glyphs { get; }

        internal void UpdateGlyphNamesCache();

        internal void SetGlyphUnicodes(Dictionary<uint, List<uint>> glyphMapping);

        IReadOnlyList<Glyph> TranslateIntoGlyphs(string input);

        Glyph GetGlyphByIndex(uint index);

        Glyph GetGlyphByName(string name);
        
        Glyph GetGlyphByUnicode(uint unicode);

        Glyph GetGlyphByCharacter(char character);

        public Int16 GetKerningValue(UInt16 leftGlyphIndex, UInt16 rightGlyphIndex);
    }
}