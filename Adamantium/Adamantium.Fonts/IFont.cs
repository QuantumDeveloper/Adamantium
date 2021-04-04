using System;
using System.Collections.Generic;

namespace Adamantium.Fonts
{
    public interface IFont
    {
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
        
        
        IReadOnlyCollection<UInt32> Unicodes { get; }
        
        IReadOnlyCollection<Glyph> Glyphs { get; }

        Glyph GetGlyphByName(string name);

        Glyph GetGlyphByUnicode(UInt32 unicode);

        Glyph GetGlyphByIndex(UInt32 index);
    }
}