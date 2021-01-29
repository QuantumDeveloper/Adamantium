using System;

namespace Adamantium.Game.GameInput
{
    public struct KeyboardInput : IEquatable<KeyboardInput>
    {
        public Keys Key;

        public InputType InputType;

        public bool Equals(KeyboardInput other)
        {
            return Key == other.Key && InputType == other.InputType;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ButtonState && Equals((ButtonState)obj);
        }
    }
}