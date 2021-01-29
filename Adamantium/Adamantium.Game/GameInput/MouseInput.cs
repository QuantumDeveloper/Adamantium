using System;
using Adamantium.Mathematics;

namespace Adamantium.Game.GameInput
{
    public struct MouseInput : IEquatable<MouseInput>
    {
        public MouseButton Button;

        public InputType InputType;

        public int WheelDelta;

        public Vector2F Delta;

        public bool Equals(MouseInput other)
        {
            return Button == other.Button && InputType == other.InputType;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is MouseButton && Equals((MouseButton)obj);
        }
    }
}