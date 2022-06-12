using Adamantium.XInput;

namespace Adamantium.Game.Core.Input
{
    internal class XBoxGamepad : Gamepad
    {
        private XBoxController _controller;
        private GamepadState state;
        
        public XBoxGamepad(XBoxController controller)
        {
            _controller = controller;
        }

        public override GamepadState GetState()
        {
            state = new GamepadState();
            state.IsConnected = _controller.IsConnected;
            var currentState = _controller.GetState();
            state.Buttons = currentState.Gamepad.Buttons;
            state.LeftThumb.X = currentState.Gamepad.LeftThumbX;
            state.LeftThumb.Y = currentState.Gamepad.LeftThumbY;
            state.RightThumb.X = currentState.Gamepad.RightThumbX;
            state.RightThumb.Y = currentState.Gamepad.RightThumbY;

            state.LeftTrigger = currentState.Gamepad.LeftTrigger;
            state.RightTrigger = currentState.Gamepad.RightTrigger;
            return state;
        }

    }
}