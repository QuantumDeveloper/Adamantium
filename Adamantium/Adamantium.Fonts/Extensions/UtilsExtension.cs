using System;
using System.Linq;
using System.Text;

namespace Adamantium.Fonts.Extensions
{
    internal static class UtilsExtension
    {
        public static float FromF2Dot14(this short value)
        {
            return (value / 16384.0f);
        }

        public static float FromF16Dot16(this int value)
        {
            return (value / 65536.0f);
        }

        public static int ToF16Dot16(this float value)
        {
            return (int)(value * 65536.0f);
        }

        public static string GetString(this uint tag)
        {
            byte[] bytes = BitConverter.GetBytes(tag).Reverse().ToArray();
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }
    }
}