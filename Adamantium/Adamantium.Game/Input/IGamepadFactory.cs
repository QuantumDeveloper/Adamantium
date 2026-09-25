namespace Adamantium.Game.Input
{
    public interface IGamepadFactory
    {
        Gamepad GetGamepad(int index);

        Gamepad[] GetConnectedGamepads();
    }
}