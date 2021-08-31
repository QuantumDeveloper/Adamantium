using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;

namespace Adamantium.Fonts
{
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
        
        public GlyphLayoutData NotDefLayoutData { get; }
        
        public IReadOnlyCollection<uint> Unicodes { get; }

        public IReadOnlyCollection<FontLanguage> LanguageSet { get; }
        
        public IReadOnlyCollection<string> Features { get; }

        public IReadOnlyCollection<Feature> EnabledFeatures { get; }

        internal void UpdateGlyphNamesCache();

        internal void SetGlyphUnicodes(Dictionary<uint, List<uint>> glyphMapping);

        internal bool IsCharacterCached(FontLanguage currentLanguage, char character);

        internal bool IsFeatureCached(FontLanguage currentLanguage, Glyph glyph, FeatureInfo featureInfo);

        internal GlyphLayoutData GetGlyphLayoutData(FontLanguage currentLanguage, FeatureInfo featureInfo, char character);

        internal void AddFeatureDataToGlyph(FontLanguage currentLanguage, FeatureInfo feature, Glyph glyph, GlyphPosition positionData);

        internal void RemoveFeatureDataFromGlyph(FontLanguage currentLanguage, FeatureInfo info, Glyph glyph);

        bool IsLanguageAvailableByMsdnName(string language);

        bool IsLanguageAvailableByIsoName(string language);

        bool IsLanguageAvailableByIsoName(string language, out FontLanguage fontLanguage);

        void AddLanguage(FontLanguage language);

        Glyph GetGlyphByIndex(uint index);

        Glyph GetGlyphByName(string name);
        
        Glyph GetGlyphByUnicode(uint unicode);

        Glyph GetGlyphByCharacter(char character);

        public Int16 GetKerningValue(UInt16 leftGlyphIndex, UInt16 rightGlyphIndex);

        public void AddFeature(Feature feature);

        public void EnableFeature(string feature, bool enable);

        public bool IsFeatureEnabled(string feature);
    }
}