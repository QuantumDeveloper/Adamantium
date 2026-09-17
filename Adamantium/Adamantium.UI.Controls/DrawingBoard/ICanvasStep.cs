namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>One undoable thing. Its own contract so that the history is not limited to what the canvas can work out by
/// COMPARING the drawing: a change that is not about where things are - a colour, a thickness, the words in a label -
/// leaves no trace in a comparison, and whoever made it is the one who knows how to take it back.</summary>
public interface ICanvasStep
{
    /// <summary>What it was, for a menu that says so.</summary>
    string Reason { get; }

    /// <summary>Put it back (<paramref name="forward"/> false) or do it again.</summary>
    void Apply(ICanvasScene scene, bool forward);
}
