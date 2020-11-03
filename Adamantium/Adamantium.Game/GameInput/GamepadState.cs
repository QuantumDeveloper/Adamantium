using Adamantium.Mathematics;
using Adamantium.XInput;

namespace Adamantium.Game.GameInput
{
    public struct GamepadState
    {
        public bool IsConnected;

        public GamepadButton Buttons;

        public Vector2F LeftThumb;

        public Vector2F RightThumb;

        public float LeftTrigger;

        public float RightTrigger;
    }
}