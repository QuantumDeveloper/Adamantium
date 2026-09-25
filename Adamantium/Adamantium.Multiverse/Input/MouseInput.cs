using Adamantium.Mathematics;

namespace Adamantium.Multiverse.Input
{
    public struct MouseInput : IEquatable<MouseInput>
    {
        public MouseButton Button;

        public InputType InputType;

        public int WheelDelta;

        public Vector2F Delta;

        /// <summary>For a press, how many clicks in a row it makes, as the host counts them: 2 for a double click.</summary>
        public int ClickCount;

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