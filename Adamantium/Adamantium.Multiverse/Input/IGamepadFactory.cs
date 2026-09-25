namespace Adamantium.Multiverse.Input
{
    public interface IGamepadFactory
    {
        Gamepad GetGamepad(int index);

        Gamepad[] GetConnectedGamepads();
    }
}