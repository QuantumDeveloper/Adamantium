namespace Adamantium.Fonts.OTF
{
    public enum PrivateDictOperatorsType : ushort
    {
        BlueValues          = 6,                            // delta
        OtherBlues          = 7,                            // delta
        FamilyBlues         = 8,                            // delta
        FamilyOtherBlues    = 9,                            // delta
        BlueScale           = (12 << 8) | 9,                // number
        BlueShift           = (12 << 8) | 10,               // number
        BlueFuzz            = (12 << 8) | 11,               // number
        StdHW               = 10,                           // number
        StdVW               = 11,                           // number
        StemSnapH           = (12 << 8) | 12,               // delta
        StemSnapV           = (12 << 8) | 13,               // delta
        ForceBold           = (12 << 8) | 14,               // boolean
        LanguageGroupe      = (12 << 8) | 17,               // number
        ExpansionFactor     = (12 << 8) | 18,               // number
        InitialRandomSeed   = (12 << 8) | 19,               // number
        Subrs               = 19,                           // number
        DefaultWidthX       = 20,                           // number
        NominalWidthX       = 21                            // number
    };
}
