namespace Adamantium.Win32.Ole;

/// <summary>The HRESULTs the OLE drag-drop contract is spoken in. Our COM objects return these directly
/// (<c>[PreserveSig]</c>), so the OS gets the exact code each method's contract expects.</summary>
public static class OleResult
{
    public const int Ok = 0;
    public const int False = 1;

    public const int NotImplemented = unchecked((int)0x80004001);
    public const int InvalidArg = unchecked((int)0x80070057);
    public const int Fail = unchecked((int)0x80004005);

    /// <summary>The requested format/aspect isn't one we offer.</summary>
    public const int FormatNotSupported = unchecked((int)0x80040064);   // DV_E_FORMATETC
    public const int TymedNotSupported = unchecked((int)0x80040069);    // DV_E_TYMED
    public const int AspectNotSupported = unchecked((int)0x80040068);   // DV_E_DVASPECT
    public const int AdviseNotSupported = unchecked((int)0x80040003);   // OLE_E_ADVISENOTSUPPORTED

    /// <summary>Returned by <c>QueryContinueDrag</c>: the button was released - complete the drop.</summary>
    public const int DragDrop = 0x00040100;
    /// <summary>Returned by <c>QueryContinueDrag</c>: Esc (or an impossible button state) - cancel the drag.</summary>
    public const int DragCancel = 0x00040101;
    /// <summary>Returned by <c>GiveFeedback</c>: let OLE draw the standard drag cursors for the current effect.</summary>
    public const int UseDefaultCursors = 0x00040102;

    /// <summary>OLE is already initialized on this thread in an INCOMPATIBLE apartment (the thread is MTA).</summary>
    public const int ChangedMode = unchecked((int)0x80010106);   // RPC_E_CHANGED_MODE
}
