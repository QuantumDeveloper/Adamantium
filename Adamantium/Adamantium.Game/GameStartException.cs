using System;

namespace Adamantium.Game
{
    public class GameStartException : Exception
    {
        public GameStartException(string message) : base(message)
        {
        }
    }
}