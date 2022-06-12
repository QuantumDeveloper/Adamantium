namespace Adamantium.Game.Core.Input
{
    internal interface IGamepadFactory
    {
        Gamepad GetGamepad(int index);

        Gamepad[] GetConnectedGamepads();
    }
}