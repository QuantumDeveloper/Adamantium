using System.Collections.Generic;
using Adamantium.Fonts.Parsers.CFF;

namespace Adamantium.Fonts.Extensions
{
    internal static class CFFParserExtensions
    {
        public static int CalculateSubrBias(this ICFFParser parser,  uint subrCount)
        {
            return subrCount switch
            {
                < 1240 => 107,
                < 33900 => 1131,
                _ => 32768
            };
        }
        
        public static void UnpackSubrToStack(
            this ICFFParser parser,
            byte[] data,
            Stack<byte> mainStack)
        {
            for (int j = data.Length; j >= 1; --j)
            {
                mainStack.Push(data[j - 1]);
            }
        }

    }
}