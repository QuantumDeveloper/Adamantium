﻿using System;

namespace Adamantium.Fonts.Exceptions
{
    public class OutlineException : Exception
    {
        public OutlineException()
        {
        }

        public OutlineException(string message) : base(message)
        {
        }

        public OutlineException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}