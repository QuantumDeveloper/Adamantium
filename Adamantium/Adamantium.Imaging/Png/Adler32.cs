namespace Adamantium.Imaging.Png
{
    public static class Adler32
    {
        public static uint GetAdler32(byte[] data)
        {
            return GetAdler32(1, data);
        }
        public static uint GetAdler32(uint adler, byte[] data)
        {
            uint s1 = adler & 0xffff;
            uint s2 = (adler >> 16) & 0xffff;
            var len = data.Length;
            int i = 0;

            while (len > 0)
            {
                /*at least 5552 sums can be done before the sums overflow, saving a lot of module divisions*/
                var amount = len > 5552 ? 5552 : len;
                len -= amount;
                while (amount > 0)
                {
                    s1 += data[i++];
                    s2 += s1;
                    --amount;
                }
                s1 %= 65521;
                s2 %= 65521;
            }

            return (s2 << 16) | s1;
        }
    }
}
