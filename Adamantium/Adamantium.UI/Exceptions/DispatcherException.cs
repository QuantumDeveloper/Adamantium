using System;

namespace Adamantium.UI.Exceptions
{
    public class DispatcherException : Exception
    {
        public DispatcherException(string? message) : base(message)
        {
        }
    }
}