namespace Adamantium.Imaging.Tga
{
    internal enum TgaImageType : byte
    {
        NoImage = 0,
        ColorMapped = 1,
        TrueColor = 2,
        BlackAndWhite = 3,
        ColorMappedRLE = 9,
        TruecolorRLE = 10,
        BlackAndWhiteRLE = 11,
    }
}