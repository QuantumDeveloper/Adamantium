namespace Adamantium.Game.GameInput
{
    internal interface IGamepadFactory
    {
        Gamepad GetGamepad(int index);

        Gamepad[] GetConnectedGamepads();
    }
}