using System.Runtime.InteropServices;

namespace Adamantium.Win32.Shell;

/// <summary>One entry of a file dialog's type list (<c>COMDLG_FILTERSPEC</c>): what the user reads, and the patterns it
/// stands for.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct FilterSpec
{
    /// <summary>The line in the drop-down, e.g. "Comma-separated values".</summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    public string Name;

    /// <summary>The patterns, semicolon-separated and wildcards included, e.g. <c>*.csv;*.txt</c>.</summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    public string Patterns;
}
