using System.Collections.Generic;
using System.ComponentModel;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>ONE SECTION of the node palette - a family of kinds under a name, as <see cref="ICanvasNodeKind.Group"/>
/// says.
/// <para>What a person looks for is "something that mixes", and a family is the answer to that. A plain object rather
/// than a control: it is what the palette's list is made OF, and a list is data.</para></summary>
public class CanvasNodeGroup : INotifyPropertyChanged
{
    private bool _expanded = true;

    internal CanvasNodeGroup(string title, IReadOnlyList<ICanvasNodeKind> kinds)
    {
        Title = title;
        Kinds = kinds;
    }

    /// <summary>The family's name.</summary>
    public string Title { get; }

    /// <summary>The kinds in it, in the order the catalogue gave them.</summary>
    public IReadOnlyList<ICanvasNodeKind> Kinds { get; }

    /// <summary>Whether the section is open. Open to begin with: a palette that has to be unfolded before anything can
    /// be read hides the very thing it was opened for.</summary>
    public bool IsExpanded
    {
        get => _expanded;
        set
        {
            if (_expanded == value) return;

            _expanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
}
