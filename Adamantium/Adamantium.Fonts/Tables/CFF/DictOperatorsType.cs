namespace Adamantium.Fonts.Tables.CFF
{
    public enum DictOperatorsType : ushort
    {
        // Top Dict operators
        version = 0,                                // SID
        Notice = 1,                                 // SID
        Copyright = (12 << 8) | 0,                  // SID        // 2 byte operator -> 12 0
        FullName = 2,                               // SID
        FamilyName = 3,                             // SID
        Weight = 4,                                 // SID
        isFixedPitch = (12 << 8) | 1,               // boolean
        ItalicAngle = (12 << 8) | 2,                // number
        UnderlinePosition = (12 << 8) | 3,          // number
        UnderlineThickness = (12 << 8) | 4,         // number
        PaintType = (12 << 8) | 5,                  // number
        CharStringType = (12 << 8) | 6,             // number
        FontMatrix = (12 << 8) | 7,                 // array
        UniqueID = 13,                              // number
        FontBBox = 5,                               // array
        StrokeWidth = (12 << 8) | 8,                // number
        XUID = 14,                                  // array
        charset = 15,                               // number
        Encoding = 16,                              // number
        CharStrings = 17,                           // number
        Private = 18,                               // number + number
        SyntheticBase = (12 << 8) | 20,             // number
        PostScript = (12 << 8) | 21,                // SID
        BaseFontName = (12 << 8) | 22,              // SID
        BaseFontBlend = (12 << 8) | 23,             // delta
        ROS = (12 << 8) | 30,                       // SID + SID + number   // Registry Ordering Supplement
        CIDFontVersion = (12 << 8) | 31,            // number
        CIDFontRevision = (12 << 8) | 32,           // number
        CIDFontType = (12 << 8) | 33,               // number
        CIDCount = (12 << 8) | 34,                  // number
        UIDBase = (12 << 8) | 35,                   // number
        FDArray = (12 << 8) | 36,                   // number
        FDSelect = (12 << 8) | 37,                  // number
        FontName = (12 << 8) | 38,                  // SID
        
        // Private Dict operators 
        BlueValues          = 6,                    // delta
        OtherBlues          = 7,                    // delta
        FamilyBlues         = 8,                    // delta
        FamilyOtherBlues    = 9,                    // delta
        BlueScale           = (12 << 8) | 9,        // number
        BlueShift           = (12 << 8) | 10,       // number
        BlueFuzz            = (12 << 8) | 11,       // number
        StdHW               = 10,                   // number
        StdVW               = 11,                   // number
        StemSnapH           = (12 << 8) | 12,       // delta
        StemSnapV           = (12 << 8) | 13,       // delta
        ForceBold           = (12 << 8) | 14,       // boolean
        LanguageGroup       = (12 << 8) | 17,       // number
        ExpansionFactor     = (12 << 8) | 18,       // number
        InitialRandomSeed   = (12 << 8) | 19,       // number
        Subrs               = 19,                   // number
        DefaultWidthX       = 20,                   // number
        NominalWidthX       = 21,                   // number
        
        // CFF 2 operators
        vsindex             = 22,                   // number
        blend               = 23,                   // delta, numberOfBlends
        vstore              = 24,                   // number
        
    }
}
