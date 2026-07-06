using System.Collections.ObjectModel;

namespace Adamantium.UI.Controls.Text;

/// <summary>The ordered inline content of a <see cref="TextBlock"/> (its <see cref="TextBlock.Inlines"/>). A typed
/// <see cref="ObservableCollection{T}"/> so the TextBlock re-lays-out when runs are added / removed.</summary>
public class InlineCollection : ObservableCollection<Inline>
{
}
