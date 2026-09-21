namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>ONE KIND A SOCKET MAY CARRY - what flows through it, and what colour that is.
/// <para>The colour belongs to the KIND and not to the socket: a graph is read by colour, so two sockets carrying the
/// same thing must look the same, and a colour set per socket is a colour that drifts the first time somebody picks
/// the wrong one. Offered as a catalogue for the same reason the node kinds are - one list, read by the inspector's
/// drop-down and by whatever paints a pin.</para></summary>
public interface ICanvasSocketKind
{
    /// <summary>The word a socket carries - "Number", "Color". Empty means "anything".</summary>
    string Kind { get; }

    /// <summary>What that looks like. Null leaves the pin wearing the theme's.</summary>
    Adamantium.Mathematics.Color? Color { get; }
}