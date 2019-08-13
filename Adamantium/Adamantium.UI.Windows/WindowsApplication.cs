using System;
using System.Collections.Generic;
using System.Text;
using Adamantium.UI.Input;

namespace Adamantium.UI.Windows
{
    public class WindowsApplication : Application
    {
        public override MouseDevice MouseDevice => WindowsMouseDevice.Instance;
        public override KeyboardDevice KeyboardDevice => WindowsKeyboardDevice.Instance;
    }
}
