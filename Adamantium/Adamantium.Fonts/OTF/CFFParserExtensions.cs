using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    internal static class CFFParserExtensions
    {
        public static int CalculateGlobalSubrBias(this ICFFParser parser,  uint subrCount)
        {
            return subrCount switch
            {
                < 1240 => 107,
                < 33900 => 1131,
                _ => 32768
            };
        }
        
        public static int CalculateLocalSubrBias(this ICFFParser parser,  uint subrCount, uint charStringType)
        {
            if (charStringType == 1)
                return 0;
            
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