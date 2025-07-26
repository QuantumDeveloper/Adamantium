using Adamantium.UI.Controls;
using Adamantium.UI.Core;

namespace Adamantium.Game.Core.Payloads
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