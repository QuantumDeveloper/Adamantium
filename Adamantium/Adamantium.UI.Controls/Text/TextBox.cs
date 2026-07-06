using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Text;

/// <summary>
/// A single-line editable text box. All editing (caret, selection, keyboard navigation, character input, clipboard,
/// mouse selection, caret blink and rendering) comes from <see cref="TextBoxBase"/>; this control just fixes the
/// single-line behaviour - Enter never inserts a newline, it raises <see cref="EnterPressed"/> (a form can submit on it).
/// A richer multi-line / rich-text editor can derive from <see cref="TextBoxBase"/> instead and reuse the same core.
/// </summary>
public class TextBox : TextBoxBase
{
    /// <summary>Raised when Enter is pressed. Single-line: the newline is not inserted; a host can commit/submit here.</summary>
    public event KeyEventHandler EnterPressed;

    protected override void OnUnhandledKey(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            EnterPressed?.Invoke(this, e);
            e.Handled = true;
        }
    }
}
