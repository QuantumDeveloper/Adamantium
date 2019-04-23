using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Adamantium.Win32;
using Adamantium.Win32.RawInput;

namespace Adamantium.Engine
{
    
    /// <summary>
    /// Internal RawInput message filtering
    /// </summary>
    internal class RawInputMessageFilter : IMessageFilter
    {
        /// <summary>
        /// WM_INPUT
        /// </summary>
        private const int WmInput = 0x00FF;

        //private InputDevice

        public virtual bool PreFilterMessage(ref System.Windows.Forms.Message m)
        {
            // Handle only WM_INPUT messages
            if (m.Msg == WmInput)
                RawInputDevice.HandleMessage(m.LParam, m.HWnd);
            return false;
        }
    }
    
}
