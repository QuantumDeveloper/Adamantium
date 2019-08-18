using System;
using System.Collections.Generic;
using System.Text;
using Adamantium.UI.Input;

namespace Adamantium.UI.Windows
{
    public class WindowsApplication : Application
    {
        internal override MouseDevice MouseDevice => WindowsMouseDevice.Instance;
        internal override KeyboardDevice KeyboardDevice => WindowsKeyboardDevice.Instance;
    }
}
