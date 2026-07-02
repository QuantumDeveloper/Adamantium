using Adamantium.UI.Core;
using Adamantium.UI.Core.Controls;

namespace Adamantium.UI.Controls;

/// <summary>
/// A reusable, self-contained piece of UI authored in AUML (a <c>&lt;View&gt;</c> root) and embedded inside other views
/// or a window (e.g. <c>&lt;local:ControlsView/&gt;</c>). The AUML code generator emits a subclass that overrides
/// <see cref="InitializeComponent"/> to build the view's own visual tree - the WPF UserControl analog.
/// </summary>
public class View : ContentControl, IView
{
    public View()
    {
        // Build the view's own tree at construction, so an embedded view is fully populated the moment its parent's
        // generated InitializeComponent does `new SomeView()`. The view-model (if the view declares x:ViewModel) is
        // resolved later, when the view attaches to the logical tree (OnAttachedToLogicalTree -> ApplyViewModel).
        InitializeComponent();
    }

    /// <summary>Builds the view's visual tree. Overridden by the generated subclass; empty by default so a plain or
    /// content-less <see cref="View"/> is valid.</summary>
    protected virtual void InitializeComponent()
    {
    }
}
