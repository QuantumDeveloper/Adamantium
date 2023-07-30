using System.Collections.Generic;
using System.Text;

namespace Adamantium.Imaging.Png.Chunks
{
    internal class iCCP : Chunk
    {
        public iCCP()
        {
            Name = "iCCP";
        }

        public string ICCPName { get; set; }

        public byte[] Profile { get; set; }

        public bool IsGrayProfile
        {
            get
            {
                if (Profile == null || Profile.Length < 20)
                {
                    return false;
                }

                var slice = Profile[16..];
                var profileStr = Encoding.ASCII.GetString(slice);
                return profileStr == "GRAY";
            }
        }

        public bool IsRGBProfile
        {
            get
            {
                if (Profile == null || Profile.Length < 20)
                {
                    return false;
                }

                var slice = Profile[16..];
                var profileStr = Encoding.ASCII.GetString(slice);
                return profileStr == "RGB ";
            }
        }

        internal override byte[] GetChunkBytes(PngState state)
        {
            var bytes = new List<byte>();

            PngCompressor compressor = new PngCompressor();
            var compressedBytes = new List<byte>();
            var result = compressor.Compress(Profile, state.EncoderSettings, compressedBytes);
            if (result > 0)
            {
                throw new PngEncoderException(result);
            }

            bytes.AddRange(GetNameAsBytes());
            var iccpProfile = Encoding.ASCII.GetBytes(ICCPName);
            if (iccpProfile.Length < 1 || iccpProfile.Length > 79)
            {
                throw new PngEncoderException(89);
            }
            bytes.AddRange(iccpProfile);
            bytes.Add(0); // null terminator
            bytes.Add(0); // compression method
            bytes.AddRange(compressedBytes);

            return bytes.ToArray();
        }

        /// <summary>
        /// It is a gray profile if bytes 16-19 are "GRAY", rgb profile if bytes 16-19
        /// are "RGB ". We do not perform any full parsing of the ICC profile here, other
        /// than check those 4 bytes to grayscale profile. Other than that, validity of
        /// the profile is not checked. This is needed only because the PNG specification
        /// requires using a non-gray color model if there is an ICC profile with "RGB "
        /// (sadly limiting compression opportunities if the input data is grayscale RGB
        /// data), and requires using a gray color model if it is "GRAY".
        /// </summary>
        public static bool IsGrayICCProfile(byte[] profile)
        {
            if (profile == null || profile.Length < 20) return false;
            var slice = profile[16..];
            return Encoding.ASCII.GetString(slice) == "GRAY";
        }

        public static bool IsRGBICCProfile(byte[] profile)
        {
            if (profile == null || profile.Length < 20) return false;
            var slice = profile[16..];
            return Encoding.ASCII.GetString(slice) == "RGB ";
        }

        internal static iCCP FromState(PngState state)
        {
            var iccp = new iCCP();
            iccp.ICCPName = state.InfoPng.IccpName;
            iccp.Profile = state.InfoPng.IccpProfile;

            return iccp;
        }
    }
}
