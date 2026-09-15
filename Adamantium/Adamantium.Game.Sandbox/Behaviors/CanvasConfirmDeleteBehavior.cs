using Adamantium.Game.Sandbox.ViewModels;
using Adamantium.Navigation;
using Adamantium.UI;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;

namespace Adamantium.Game.Sandbox.Behaviors;

/// <summary>Asks before anything leaves the drawing.
/// <code>&lt;InfiniteCanvas.Behaviors&gt;&lt;local:CanvasConfirmDeleteBehavior Asks="{Binding AsksBeforeDelete}"/&gt;&lt;/InfiniteCanvas.Behaviors&gt;</code>
/// </summary>
/// <remarks>
/// Why a behavior, and why here rather than in a command. A dialog is ANSWERED LATER, and a key press cannot be held
/// open while it is: the canvas therefore hands the deletion over - it raises the request, the handler says "mine", and
/// the handler deletes when it has an answer. That handler has to reach both the dialog service and the canvas itself,
/// and the only thing that sees both is a behavior on the canvas. A view model reaching for the control would be worse,
/// and code-behind is not an option in this application at all.
///
/// Off, this does nothing at all: it leaves Handled alone and the canvas deletes exactly as it always has.
/// </remarks>
public class CanvasConfirmDeleteBehavior : Behavior<InfiniteCanvas>
{
    public static readonly AdamantiumProperty AsksProperty = AdamantiumProperty.Register(nameof(Asks),
        typeof(bool), typeof(CanvasConfirmDeleteBehavior), new PropertyMetadata(true));

    /// <summary>Whether a deletion is worth a question. A switch rather than a rule: with undo in place, being asked
    /// about every single object is noise - and nobody can know in advance which of the two a given drawing wants.</summary>
    public bool Asks
    {
        get => GetValue<bool>(AsksProperty);
        set => SetValue(AsksProperty, value);
    }

    protected override void OnAttached(InfiniteCanvas canvas) => canvas.DeleteRequested += OnDeleteRequested;

    protected override void OnDetached(InfiniteCanvas canvas) => canvas.DeleteRequested -= OnDeleteRequested;

    private async void OnDeleteRequested(object sender, CanvasDeleteRequestedEventArgs e)
    {
        if (!Asks || sender is not InfiniteCanvas canvas) return;

        var dialogs = UIApplication.Current?.Container?.Resolve<IDialogService>();
        if (dialogs == null) return;

        // Taken over BEFORE the first await: the canvas is about to look at this flag, and it will not wait.
        e.Handled = true;

        var many = e.Items.Count > 1;
        var result = await dialogs.ShowDialogAsync<ConfirmDialogViewModel>(new NavigationParameters()
            .Add("title", many ? "Delete objects" : "Delete object")
            .Add("message", many ? $"Remove {e.Items.Count} objects from the drawing?" : "Remove it from the drawing?"));

        if (result.Result == DialogButtonResult.Ok) canvas.DeleteSelection();
    }
}
