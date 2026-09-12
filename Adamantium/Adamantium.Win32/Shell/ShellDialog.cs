using System;

namespace Adamantium.Win32.Shell;

/// <summary>The identifiers and flags the shell's file dialogs are addressed by.</summary>
public static class ShellDialog
{
    /// <summary>CLSID_FileSaveDialog.</summary>
    public static readonly Guid ClsidFileSaveDialog = new("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B");

    /// <summary>IID_IFileSaveDialog.</summary>
    public static readonly Guid IidFileSaveDialog = new("84BCCD23-5FDE-4CDB-AEA4-AF64B83D78AB");

    /// <summary>FOS_OVERWRITEPROMPT - ask before replacing a file that is already there. A save dialog sets this
    /// itself, but only until someone calls SetOptions, which replaces the lot.</summary>
    public const uint OverwritePrompt = 0x00000002;

    /// <summary>FOS_FORCEFILESYSTEM - only let the user choose somewhere a path can be opened. Without it the dialog
    /// also offers the parts of the shell namespace that are not files at all.</summary>
    public const uint ForceFileSystem = 0x00000040;

    /// <summary>SIGDN_FILESYSPATH - the full path on disk, which is the only name we ever want back.</summary>
    public const uint SigdnFileSysPath = 0x80058000;

    /// <summary>HRESULT_FROM_WIN32(ERROR_CANCELLED) - what Show answers when the user closed the dialog without
    /// choosing. A refusal, not a failure.</summary>
    public const int Cancelled = unchecked((int)0x800704C7);
}
