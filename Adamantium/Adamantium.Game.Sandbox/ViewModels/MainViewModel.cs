using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adamantium.MVVM;
using Adamantium.Navigation;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.Win32;

namespace Adamantium.Game.Sandbox.ViewModels;

[ViewModel]
public partial class MainViewModel
{
    private readonly INavigationService _navigation;
    private readonly WindowDemoSettings _settings;

    public MainViewModel(INavigationService navigation, WindowDemoSettings settings)
    {
        _navigation = navigation;
        _settings = settings;
    }

    // Opens the SECOND window entirely from the view model: name the "workspace" shell (a custom WorkspaceWindow) and the
    // framework creates it and loads WorkspaceShellViewModel's view - this VM never touches a Window type. The
    // single-instance mode follows the shared setting (toggled from the Navigation tab).
    [Command] private Task OpenWorkspace() =>
        _navigation.OpenWindowAsync<WorkspaceShellViewModel>(windowShell: "workspace", singleInstance: !_settings.AllowDuplicateWindows);

    [Command]
    private void ShowMessage()
    {
        Width += 150;
    }

    [Bindable]
    private double _width = 150;

    // Shows the permanent diagnostics PLATE (top-right overlay). Driven by the right-side Diagnostics ToggleSwitch.
    [Bindable] private bool _diagnosticsOpen;

    [Command] private void ToggleDiagnostics() => DiagnosticsOpen = !DiagnosticsOpen;

    // Opens the diagnostics SLIDEPANEL (slides in from the right). Driven by the LEFT caption "Diagnostics" command -
    // kept separate from the plate so the two diagnostics surfaces are toggled independently.
    [Bindable] private bool _diagnosticsPanelOpen;

    [Command] private void ToggleDiagnosticsPanel() => DiagnosticsPanelOpen = !DiagnosticsPanelOpen;

    // Last title-bar command that ran - shown in the content so the demo commands do something visible AND distinct.
    [Bindable] private string _lastCommand = "(none)";

    // One parameterized command the caption buttons share; the CommandParameter says which action it was.
    [Command] private void RunAction(string name) => LastCommand = name;

    // Window resize mode (bound two-way to Window.ResizeMode) + a toggle between full edge resize and grip-only resize
    // (the borderless ResizeGripper in the bottom-right corner).
    [Bindable] private WindowResizeMode _windowResizeMode = WindowResizeMode.CanResize;

    [Command] private void ToggleGripResize() =>
        WindowResizeMode = WindowResizeMode == WindowResizeMode.CanResizeWithGrip
            ? WindowResizeMode.CanResize
            : WindowResizeMode.CanResizeWithGrip;

    // Right-side caption command bar (bound to Window.RightWindowCommands). Deliberately several items so they overflow
    // into the "..." menu when the window is narrowed - the resize-smaller use-case. Built lazily so the generated
    // commands exist by first bind.
    // Caption commands as VECTOR icons (SVG-style stroked paths in a ~14x14 box) + hover tooltips - the modern look.
    private List<WindowCommand> _rightWindowCommands;
    public IEnumerable RightWindowCommands => _rightWindowCommands ??= new()
    {
        new WindowCommand { IconData = "M7,2 L7,12 M2,7 L12,7",                    Label = "New",  ToolTip = "New file",  Command = RunActionCommand, CommandParameter = "New" },
        new WindowCommand { IconData = "M0,3 L5,3 L6,1 L13,1 L13,13 L0,13 Z",      Label = "Open", ToolTip = "Open",      Command = RunActionCommand, CommandParameter = "Open" },
        new WindowCommand { IconData = "M7,1 L7,9 M3,5 L7,9 L11,5 M1,13 L13,13",   Label = "Save", ToolTip = "Save",      Command = RunActionCommand, CommandParameter = "Save" },
        new WindowCommand { IconData = "M5,2 L1,7 L5,12 M1,7 L13,7",               Label = "Undo", ToolTip = "Undo",      Command = RunActionCommand, CommandParameter = "Undo" },
        new WindowCommand { IconData = "M1,2 L13,2 L13,12 L1,12 Z M1,5 L13,5",     Label = "Workspace", ToolTip = "Open workspace window", Command = OpenWorkspaceCommand },
        new WindowCommand { IconData = "M7,0 L14,7 L7,14 L0,7 Z",                  Label = "Help", ToolTip = "Help",      Command = RunActionCommand, CommandParameter = "Help" },
    };

    private List<WindowCommand> _leftWindowCommands;
    public IEnumerable LeftWindowCommands => _leftWindowCommands ??= new()
    {
        // A real, distinct action to show variety: "Diagnostics" toggles the side panel; "File" just reports itself.
        new WindowCommand { IconData = "M1,13 L1,7 M6,13 L6,2 M11,13 L11,9",       Label = "Diagnostics", ToolTip = "Toggle diagnostics panel", Command = ToggleDiagnosticsPanelCommand },
        new WindowCommand { IconData = "M2,0 L9,0 L12,3 L12,14 L2,14 Z M9,0 L9,3 L12,3", Label = "File",  ToolTip = "File",              Command = RunActionCommand, CommandParameter = "File" },
        // Grip-only resize toggle - a diagonal resize double-arrow.
        new WindowCommand { IconData = "M2,2 L12,12 M12,12 L12,8 M12,12 L8,12 M2,2 L2,6 M2,2 L6,2", Label = "Resize mode", ToolTip = "Toggle grip-only resize", Command = ToggleGripResizeCommand },
    };
}