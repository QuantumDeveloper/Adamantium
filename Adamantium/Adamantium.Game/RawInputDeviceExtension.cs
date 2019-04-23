using System;
using System.Windows.Forms;

namespace Adamantium.Engine
{
    
    public static class RawInputDeviceExtension
    {
        private static RawInputMessageFilter filter;
        static RawInputDeviceExtension()
        {
            filter = new RawInputMessageFilter();
        }
        public static void EnableFiltering(IntPtr target, FilteringOptions options)
        {
            if (options != FilteringOptions.NoFiltering)
            {
                if (options == FilteringOptions.Default)
                {
                    Application.AddMessageFilter(filter);
                }
                else
                {
                    MessageFilterHook.AddMessageFilter(target, filter);
                }
            }
        }

        public static void AddMessageFilter(IntPtr target)
        {
            MessageFilterHook.AddMessageFilter(target, filter);
        }

        public static void RemoveMessageFilter(IntPtr target)
        {
            MessageFilterHook.RemoveMessageFilter(target, filter);
        }
    }
}
