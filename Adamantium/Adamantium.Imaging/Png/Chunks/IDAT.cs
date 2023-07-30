using System.Collections.Generic;

namespace Adamantium.Imaging.Png.Chunks
{
    internal class IDAT : Chunk
    {
        public IDAT()
        {
            Name = "IDAT";
        }

        /// <summary>
        /// Raw color data in RGBARGBARGBA...
        /// </summary>
        public byte[] RawData { get; set; }

        internal override byte[] GetChunkBytes(PngState state)
        {
            var bytes = new List<byte>();
            PngCompressor compressor = new PngCompressor();
            var compressedData = new List<byte>();
            var result = compressor.Compress(RawData, state.EncoderSettings, compressedData);
            if (result > 0)
            {
                throw new PngEncoderException(result);
            }

            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(compressedData);
 
            return bytes.ToArray();
        }

        internal static IDAT FromState(PngState state, byte[] rawData)
        {
            var data = new IDAT();
            data.RawData = rawData;
            return data;
        }
    }
}
