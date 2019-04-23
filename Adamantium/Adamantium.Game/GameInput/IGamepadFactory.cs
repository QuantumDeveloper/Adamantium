using Adamantium.XInput;

namespace Adamantium.Engine.GameInput
{
    internal interface IGamepadFactory
    {
        Gamepad GetGamepad(int index);

        Gamepad[] GetConnectedGamepads();
    }
}