using System.Runtime.InteropServices;

namespace Adamantium.Imaging.Dds
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Header
    {
        public int Size;
        public HeaderFlags Flags;
        public int Height;
        public int Width;
        public int PitchOrLinearSize;
        public int Depth; // only if DDS_HEADER_FLAGS_VOLUME is set in dwFlags
        public int MipMapCount;

        private readonly uint unused1;
        private readonly uint unused2;
        private readonly uint unused3;
        private readonly uint unused4;
        private readonly uint unused5;
        private readonly uint unused6;
        private readonly uint unused7;
        private readonly uint unused8;
        private readonly uint unused9;
        private readonly uint unused10;
        private readonly uint unused11;

        public PixelFormat PixelFormat;
        public SurfaceFlags SurfaceFlags;
        public CubemapFlags CubemapFlags;

        private readonly uint Unused12;
        private readonly uint Unused13;

        private readonly uint Unused14;
    }
}