using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    public abstract class OperandParser
    {
        protected GenericOperandResult Number(byte b0, Stack<byte> mainStack)
        {
            if (b0 == 28) // shortint operator. This value should be casted to short, otherwise it could be interpreted incorrectly 
            {
                var b1 = mainStack.Pop();
                var b2 = mainStack.Pop();

                return (short)((b1 << 8) | b2);
            }

            if (b0 >= 32 && b0 <= 246)
            {
                return b0 - 139;
            }
            if (b0 >= 247 && b0 <= 250)
            {
                var b1 = mainStack.Pop();

                return (b0 - 247) * 256 + b1 + 108;
            }
            if (b0 >= 251 && b0 <= 254)
            {
                var b1 = mainStack.Pop();

                return -(b0 - 251) * 256 - b1 - 108;
            }
            if (b0 == 255)
            {
                var b1 = mainStack.Pop();
                var b2 = mainStack.Pop();
                var b3 = mainStack.Pop();
                var b4 = mainStack.Pop();

                var fixedPointNumber = (b1 << 24) | (b2 << 16) | (b3 << 8) | b4;

                return (fixedPointNumber / 65536.0);
            }
            throw new ArgumentException($"'b0' = {b0} is out of range!");
        }
    }
}
