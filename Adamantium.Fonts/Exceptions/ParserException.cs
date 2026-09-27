using System;

namespace Adamantium.Fonts.Exceptions
{
    internal class ParserException : Exception
    {
        public ParserException(string message)
            : base(message)
        {
        }
    }
}
