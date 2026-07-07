using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>
/// One item in a window's <see cref="WindowBase.LeftWindowCommands"/> / <see cref="WindowBase.RightWindowCommands"/> -
/// a caption-bar command. <see cref="Content"/> is what the bar button shows (an icon/glyph/short text); <see cref="Label"/>
/// is the text used when the command spills into the overflow menu. A plain data item so a view-model can expose a
/// collection of them and bind it to the window.
/// </summary>
public class WindowCommand
{
    /// <summary>Vector icon for the caption BAR button - an SVG-style path geometry string (stroked). Preferred over
    /// <see cref="Content"/> for a modern icon bar; the overflow menu still shows <see cref="OverflowLabel"/> text.</summary>
    public string IconData { get; set; }

    private Geometry _icon;
    private bool _iconParsed;

    /// <summary>The parsed <see cref="IconData"/> geometry, ready to bind to a Path's Data (a binding does not run the
    /// string->Geometry conversion the AUML parser does, so parse it here, once, via the same converter).</summary>
    public Geometry Icon
    {
        get
        {
            if (!_iconParsed)
            {
                _iconParsed = true;
                _icon = string.IsNullOrEmpty(IconData) ? null : TypeCastFactory.CastFromString(IconData, typeof(Geometry)) as Geometry;
            }
            return _icon;
        }
    }

    /// <summary>Fallback text/content for the bar button when <see cref="IconData"/> is not set.</summary>
    public object Content { get; set; }

    /// <summary>Text shown for this command in the OVERFLOW menu (falls back to <see cref="Content"/> if unset).</summary>
    public string Label { get; set; }

    /// <summary>Invoked when the command's bar button or overflow row is clicked.</summary>
    public ICommand Command { get; set; }

    public object CommandParameter { get; set; }

    /// <summary>Optional hover tooltip for the bar button.</summary>
    public string ToolTip { get; set; }

    /// <summary>The overflow-menu label - <see cref="Label"/>, else the <see cref="Content"/> text.</summary>
    public object OverflowLabel => Label ?? Content;
}
