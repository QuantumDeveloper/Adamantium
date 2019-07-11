namespace Adamantium.Imaging.Bmp
{
    internal static class ColorMasks
    {
        //565 RGB masks
        public static class RGB565
        {
            public static ushort RedMask => 0xF800;
            public static ushort GreenMask => 0x7E0;
            public static ushort BlueMask => 0x1F;
        }

        //555 RGB masks
        public static class RGB555
        {
            public static ushort RedMask => 0x7C00;
            public static ushort GreenMask => 0x3E0;
            public static ushort BlueMask => 0x1F;
        }

        public static class R8G8B8A8
        {
            public static uint RedMask => 0x00ff0000;
            public static uint GreenMask => 0x0000ff00;
            public static uint BlueMask => 0x000000ff;
            public static uint AlphaMask => 0xff000000;
            public static uint ColorSpaceType => 0x73524742;
        }
    }
}