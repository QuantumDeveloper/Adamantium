using System;

namespace Adamantium.XInput
{
    /// <summary>
    /// Future devices may return HID codes and virtual key values that are not supported on current devices, and are currently undefined.  Applications should ignore these unexpected values.
    /// A virtual-key code is a byte value that represents a particular physical key on the keyboard, not the character or characters (possibly none) 
    /// that the key can be mapped to based on keyboard state.The keyboard state at the time a virtual key is pressed modifies the character reported.
    /// For example, VK_4 might represent a "4" or a "$", depending on the state of the SHIFT key.
    /// A reported keyboard event includes the virtual key that caused the event, whether the key was pressed or released (or is repeating), and the state of the keyboard at the time of the event. 
    /// The keyboard state includes information about whether any CTRL, ALT, or SHIFT keys are down.
    /// If the keyboard event represents an Unicode character (for example, pressing the "A" key), the Unicode member will contain that character. Otherwise, Unicode will contain the value zero.
    /// The valid virtual-key (VK_xxx) codes are defined in XInput.h. In addition to codes that indicate key presses, the following codes indicate controller input.
    /// </summary>
    [Flags]
    public enum GamepadKeyCode : short
    {
        None = 0,

        /// <summary>
        /// A button 
        /// </summary>
        A = 22528,

        /// <summary>
        /// B button 
        /// </summary>
        B = 22529,

        /// <summary>
        /// X button 
        /// </summary>
        X = 22530,

        /// <summary>
        /// Y button 
        /// </summary>
        Y = 22531,

        /// <summary>
        /// Right shoulder button 
        /// </summary>
        RightShoulder = 22532,

        /// <summary>
        /// Left shoulder button 
        /// </summary>
        LeftShoulder = 22533,

        /// <summary>
        /// Left trigger 
        /// </summary>
        LeftTrigger = 22534,

        /// <summary>
        /// Right trigger 
        /// </summary>
        RightTrigger = 22535,

        /// <summary>
        /// Directional pad up 
        /// </summary>
        DpadUp = 22544,

        /// <summary>
        /// Directional pad down 
        /// </summary>
        DpadDown = 22545,

        /// <summary>
        /// Directional pad left 
        /// </summary>
        DpadLeft = 22546,

        /// <summary>
        /// Directional pad right 
        /// </summary>
        DpadRight = 22547,

        /// <summary>
        /// START button 
        /// </summary>
        Start = 22548,

        /// <summary>
        /// BACK button 
        /// </summary>
        Back = 22549,
        
        /// <summary>
        /// Left thumbstick click
        /// </summary>
        LeftThumbPress = 22550,

        /// <summary>
        /// Right thumbstick click 
        /// </summary>
        RightThumbPress = 22551,

        /// <summary>
        /// Left thumbstick up 
        /// </summary>
        LeftThumbUp = 22560,

        /// <summary>
        /// Left thumbstick down 
        /// </summary>
        LeftThumbDown = 22561,

        /// <summary>
        /// Left thumbstick right 
        /// </summary>
        LeftThumbRight = 22562,

        /// <summary>
        /// Left thumbstick left 
        /// </summary>
        LeftThumbLeft = 22563,

        /// <summary>
        /// Left thumbstick up and left 
        /// </summary>
        LeftThumbUpleft = 22564,

        /// <summary>
        /// Left thumbstick up and right 
        /// </summary>
        LeftThumbUpright = 22565,

        /// <summary>
        /// Left thumbstick down and right 
        /// </summary>
        LeftThumbDownright = 22566,

        /// <summary>
        /// Left thumbstick down and left 
        /// </summary>
        LeftThumbDownleft = 22567,

        /// <summary>
        /// Right thumbstick up 
        /// </summary>
        RightThumbUp = 22576,

        /// <summary>
        /// Right thumbstick down 
        /// </summary>
        RightThumbDown = 22577,

        /// <summary>
        /// Right thumbstick right 
        /// </summary>
        RightThumbRight = 22578,

        /// <summary>
        /// Right thumbstick left 
        /// </summary>
        RightThumbLeft = 22579,

        /// <summary>
        /// Right thumbstick up and left 
        /// </summary>
        RightThumbUpleft = 22580,

        /// <summary>
        /// Right thumbstick up and right 
        /// </summary>
        RightThumbUpright = 22581,

        /// <summary>
        /// Right thumbstick down and right 
        /// </summary>
        RightThumbDownright = 22582,

        /// <summary>
        /// Right thumbstick down and left 
        /// </summary>
        RightThumbDownleft = 22583
    }
}