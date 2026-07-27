namespace Adamantium.UI.Core.Input.Raw;

/// <summary>
/// What a pointer event IS, named by meaning. The values are our own and deliberately arbitrary: they used to be the
/// Win32 message numbers, which let the platform cast a message straight into this enum - convenient, and wrong twice
/// over. It tied a platform-neutral type to one OS's numbering, and it hid a collision: <see cref="EnterWindow"/> had
/// no value of its own and so silently took <c>MouseMove + 1</c> - which is exactly <see cref="LeftButtonDown"/>.
/// Nothing produced EnterWindow, so nothing had gone wrong yet.
/// </summary>
public enum RawMouseEventType : uint
{
   MouseMove,
   /// <summary>The pointer entered the window. No platform raises this yet - the over-chain is derived from moves.</summary>
   EnterWindow,
   LeaveWindow,

   LeftButtonDown,
   LeftButtonUp,
   RightButtonDown,
   RightButtonUp,
   MiddleButtonDown,
   MiddleButtonUp,
   X1ButtonDown,
   X1ButtonUp,
   X2ButtonDown,
   X2ButtonUp,

   LeftButtonDoubleClick,
   RightButtonDoubleClick,
   MiddleButtonDoubleClick,

   MouseWheel,

   /// <summary>Relative motion for a game's mouse-look: an unbounded delta instead of a position, produced while the
   /// cursor is hidden and held centred (see <c>IWindowWorkerService.SetRelativeMouseMode</c>).</summary>
   RawMouseMove,
   RawLeftButtonDown,
   RawRightButtonDown,
   RawMiddleButtonDown,
   RawLeftButtonUp,
   RawRightButtonUp,
   RawMiddleButtonUp,
}
