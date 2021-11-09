using Adamantium.UI.Controls;

namespace Adamantium.Game.Events
{
    public class WindowStatePayload
    {
        public WindowState State { get; }

        public WindowStatePayload(WindowState state)
        {
            State = state;
        }
    }
}