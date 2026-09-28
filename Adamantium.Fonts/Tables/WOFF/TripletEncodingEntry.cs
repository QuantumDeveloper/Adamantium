namespace Adamantium.Fonts.Tables.WOFF
{
    internal class TripletEncodingEntry
    {
        public readonly byte ByteCount;
        public readonly byte XBits;
        public readonly byte YBits;
        public readonly ushort DeltaX;
        public readonly ushort DeltaY;
        public readonly sbyte XSign;
        public readonly sbyte YSign;

        public TripletEncodingEntry(
            byte byteCount,
            byte xBits,    
            byte yBits,    
            ushort deltaX, 
            ushort deltaY, 
            sbyte xSign,
            sbyte ySign)
        {
            ByteCount = byteCount;
            XBits = xBits;
            YBits = yBits;
            DeltaX = deltaX;
            DeltaY = deltaY;
            XSign = xSign;
            YSign = ySign;
        }

        /// <summary>
        /// Translate X coordinate
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public int TranslateX(int x) => (x + DeltaX) * XSign;
        
        /// <summary>
        /// Translate Y coordinate
        /// </summary>
        /// <param name="y"></param>
        /// <returns></returns>
        public int TranslateY(int y) => (y + DeltaY) * YSign;
    }
}