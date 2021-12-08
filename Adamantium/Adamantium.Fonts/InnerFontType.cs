namespace Adamantium.Fonts
{
    public enum InnerFontType : uint
    {
        TrueType = 0x00010000, // TTF
        SingleOTFFont = 0x4F54544F, // OTTO
        OTFFontCollection = 0x74746366, // ttcf
    }
}