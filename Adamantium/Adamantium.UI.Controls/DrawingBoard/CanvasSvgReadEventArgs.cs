using System;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>What came of reading or writing a drawing file.
/// <para>An EVENT and not a message box: the canvas knows what happened and the application knows how this program
/// talks to people - in a status line, a toast, a dialog, or not at all. A control that decided that for everybody
/// would be wrong for most of them.</para></summary>
public sealed class CanvasSvgReadEventArgs(int read, int skipped, string trouble) : EventArgs
{
    /// <summary>How many items came in.</summary>
    public int Read { get; } = read;

    /// <summary>How many elements of the file were passed over - an arc, a gradient, something the format grew after
    /// this was written. Not an error: the rest of the drawing is there, and this is what a person needs to be told
    /// before they save it back over the original.</summary>
    public int Skipped { get; } = skipped;

    /// <summary>What went wrong, where something did - a file that could not be read, a folder that refused. Null when
    /// nothing did.</summary>
    public string Trouble { get; } = trouble;

    public bool Failed => Trouble != null;
}
