namespace Adamantium.Fonts.Extensions
{
    internal static class Woff2Utils
    {
        public static byte[] ExpandBitmap(this byte[] bboxBitmap)
        {
            byte[] expandArr = new byte[bboxBitmap.Length * 8];

            int index = 0;
            for (int i = 0; i < bboxBitmap.Length; ++i)
            {
                byte b = bboxBitmap[i];
                expandArr[index++] = (byte)((b >> 7) & 0x1);
                expandArr[index++] = (byte)((b >> 6) & 0x1);
                expandArr[index++] = (byte)((b >> 5) & 0x1);
                expandArr[index++] = (byte)((b >> 4) & 0x1);
                expandArr[index++] = (byte)((b >> 3) & 0x1);
                expandArr[index++] = (byte)((b >> 2) & 0x1);
                expandArr[index++] = (byte)((b >> 1) & 0x1);
                expandArr[index++] = (byte)((b >> 0) & 0x1);
            }
            return expandArr;
        }
    }
}