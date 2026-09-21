using System;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Text;

/// <summary>
/// Base for a piece of inline text inside a <see cref="TextBlock"/> (see <see cref="Run"/>). An inline is a
/// <see cref="FundamentalUIComponent"/>, so it lives in the logical tree and INHERITS the TextBlock's DataContext - which
/// is exactly why its properties are bindable (<c>&lt;Run Text="{Binding ...}"/&gt;</c>) unlike WPF's Run, where binding a
/// Run's Text is awkward. The hosting TextBlock listens to <see cref="Changed"/> to re-shape when a bound value updates.
/// </summary>
public abstract class Inline : FundamentalUIComponent
{
    /// <summary>Raised when a property that affects this inline's rendered text changes (so the TextBlock re-lays-out).</summary>
    internal event EventHandler Changed;

    protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
