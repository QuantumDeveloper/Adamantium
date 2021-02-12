using System;

namespace Adamantium.Fonts.Common
{
    internal class ParserException : Exception
    {
        public ParserException(string message)
            : base(message)
        {
        }
    }
}
