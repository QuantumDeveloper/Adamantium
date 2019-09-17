using System;

namespace Adamantium.MacOS
{
    [Flags]
    public enum OSXWindowStyle : uint
    {
        Borderless = 0,
        Titled = 1,
        Closable = 2,
        Miniaturizable = 4,
        Resizable = 8,
        UnifiedTitleAndToolbar = 1 << 12,
        FullScreen = 1 << 14,
        FullSizeContentView = 1 << 15,
        UtilityWindow			= 1 << 4,
        DocModalWindow 		= 1 << 6,
        NonactivatingPanel	= 1 << 7, // Specifies that a panel that does not activate the owning application
        HUDWindow = 1 << 13
            
    }
}