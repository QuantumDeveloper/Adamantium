using Adamantium.XInput;

namespace Adamantium.Engine.GameInput
{
    internal class XBoxGamepad : Gamepad
    {
        private XBoxController _controller;
        private GamepadState _state;
        
        public XBoxGamepad(XBoxController controller)
        {
            _controller = controller;
        }

        public override GamepadState GetState()
        {
            _state = new GamepadState();
            _state.IsConnected = _controller.IsConnected;
            var currentState = _controller.GetState();
            _state.Buttons = currentState.Gamepad.Buttons;
            _state.LeftThumb.X = currentState.Gamepad.LeftThumbX;
            _state.LeftThumb.Y = currentState.Gamepad.LeftThumbY;
            _state.RightThumb.X = currentState.Gamepad.RightThumbX;
            _state.RightThumb.Y = currentState.Gamepad.RightThumbY;

            _state.LeftTrigger = currentState.Gamepad.LeftTrigger;
            _state.RightTrigger = currentState.Gamepad.RightTrigger;
            return _state;
        }

    }
}