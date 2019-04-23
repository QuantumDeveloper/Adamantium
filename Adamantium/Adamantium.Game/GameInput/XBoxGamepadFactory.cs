using System.Collections.Generic;
using Adamantium.XInput;

namespace Adamantium.Engine.GameInput
{
    internal class XBoxGamepadFactory : IGamepadFactory
    {
        private static Gamepad[] Gamepads;
        private static XBoxController[] Controllers;

        private const int MaxXBoxControllersCount = 4;

        public XBoxGamepadFactory()
        {
            Gamepads = new Gamepad[4];
            Controllers = new XBoxController[4];

            for (int i = 0; i < MaxXBoxControllersCount; ++i)
            {
                var controller = new XBoxController((UserIndex)i);
                Controllers[i] = controller;
                Gamepads[i] = new XBoxGamepad(controller);
            }
        }

        public Gamepad GetGamepad(int index)
        {
            return Gamepads[index];
        }

        public Gamepad[] GetConnectedGamepads()
        {
            List<Gamepad> gamepads = new List<Gamepad>();
            for (var i = 0; i < Controllers.Length; i++)
            {
                if (Controllers[i].IsConnected)
                {
                    gamepads.Add(Gamepads[i]);
                }
            }
            return gamepads.ToArray();
        }
    }
}