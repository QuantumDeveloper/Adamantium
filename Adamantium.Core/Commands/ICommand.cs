using System;

namespace Adamantium.Core.Commands;

/// <summary>
/// The binding contract a control (e.g. <c>Button</c>) targets. The optional <paramref name="parameter"/> is the
/// untyped value the UI passes (a control's CommandParameter) — type-safe commands live as the generic
/// <see cref="ICommand{T}"/> for code, but the UI boundary is inherently untyped.
/// </summary>
public interface ICommand
{
    bool CanExecute(object parameter = null);

    void Execute(object parameter = null);

    /// <summary>Raised when <see cref="CanExecute"/>'s result may have changed, so bound controls re-query it.</summary>
    event EventHandler CanExecuteChanged;

    /// <summary>Re-raises <see cref="CanExecuteChanged"/>. On the contract so a holder can refresh any command
    /// without knowing its concrete type (no static CommandManager, no global re-query storm).</summary>
    void RaiseCanExecuteChanged();
}
