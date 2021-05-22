using System;
using System.Linq;
using System.Text;

namespace Adamantium.Fonts.Extensions
{
    internal static class UtilsExtension
    {
        public static float ToF2Dot14(this short value)
        {
            return (value / 16384.0f);
        }
        
        public static string GetString(this uint tag)
        {
            byte[] bytes = BitConverter.GetBytes(tag).Reverse().ToArray();
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }
    }
}