using System.Collections;
using System.ComponentModel;
using Adamantium.Core.Commands;
using Adamantium.UI.Controls;
using Adamantium.UI.Core.Templates;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>What THIS shell puts in its quick-access bar. A caption command plus what the bar adds: a command that is
/// not a button carries its own compact form, and a drop-down carries the rows it drops. The engine states only the
/// interface - what the item is made of is the application's to decide.</summary>
public class QuickAccessCommand : WindowCommand, IQuickAccessItem, INotifyPropertyChanged
{
    public DataTemplate QuickAccessTemplate { get; set; }

    public IEnumerable DropDownItems { get; set; }

    public DataTemplate DropDownItemTemplate { get; set; }

    /// <summary>Which command of the shell this stands for - carried over from the request that added it, so a request
    /// to take it back out names the same one.</summary>
    public object Key { get; set; }

    /// <summary>What the caption button already runs - the bar's item and the ribbon's command are the same one.</summary>
    public ICommand Action => Command;

    /// <summary>ON/OFF for a command that has such a state, null for one that has not. It is the SHELL that says this,
    /// not the engine: the ribbon holds a visual of a command, and a visual has no business owning state it only shows.
    /// <para>The bar's toggle template binds this two-way, so the caption button and the ribbon button are two views of
    /// one value - see <see cref="ShellQuickAccessTemplateSelector"/>.</para></summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;

            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    private bool? _isChecked;

    public event PropertyChangedEventHandler PropertyChanged;
}
