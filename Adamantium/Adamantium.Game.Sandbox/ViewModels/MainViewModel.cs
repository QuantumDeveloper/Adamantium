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
    private readonly IDialogService _dialogs;

    public MainViewModel(INavigationService navigation, WindowDemoSettings settings, IDialogService dialogs)
    {
        _navigation = navigation;
        _settings = settings;
        _dialogs = dialogs;
    }

    // The dialog host follows the Navigation-tab toggle: an in-window overlay, or a separate window.
    private DialogHostKind DialogHost => _settings.DialogsAsWindows ? DialogHostKind.Window : DialogHostKind.Overlay;

    // Shows a confirm dialog (IDialogService), awaits the result and reports it - the VM-first dialog round-trip from a
    // title-bar command, hosted as an overlay or a window per the toggle.
    [Command] 
    private async Task ShowConfirm()
    {
        var result = await _dialogs.ShowDialogAsync<ConfirmDialogViewModel>(
            new NavigationParameters().Add("message", "Apply changes to the current scene?"), DialogHost);
        LastCommand = $"Dialog: {result.Result}";
    }

    // The real Help/About: product/version/manufacturer read from the assembly metadata.
    [Command] private Task ShowAbout() => _dialogs.ShowDialogAsync<AboutDialogViewModel>(host: DialogHost);

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

    // Analytic AA (the coverage fringe on fills + feathered strokes), bound to the window's AnalyticAntialiasing. It
    // lives in the diagnostics panel rather than the chrome because it is a comparison switch, not a setting: the flag
    // is re-read per frame, so flipping it re-renders WITHOUT rebuilding the render cache - which is what makes an A/B
    // of an edge artifact possible at all.
    [Bindable] private bool _analyticAa = true;

    // Last dialog result - shown in the window content (written by ShowConfirm).
    [Bindable] private string _lastCommand = "(none)";

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
        new WindowCommand { IconData = "M1,2 L13,2 L13,12 L1,12 Z M1,5 L13,5",     Label = "Workspace", ToolTip = "Open workspace window", Command = OpenWorkspaceCommand },
        new WindowCommand { IconData = "M1,2 L13,2 L13,12 L1,12 Z M4,6 L10,6 M4,9 L8,9", Label = "Dialog", ToolTip = "Show a confirm dialog", Command = ShowConfirmCommand },
        new WindowCommand { IconData = "M7,0 L14,7 L7,14 L0,7 Z",                  Label = "Help", ToolTip = "About Adamantium Sandbox", Command = ShowAboutCommand },
    };

    private List<WindowCommand> _leftWindowCommands;
    public IEnumerable LeftWindowCommands => _leftWindowCommands ??= new()
    {
        new WindowCommand { IconData = "M1,13 L1,7 M6,13 L6,2 M11,13 L11,9",       Label = "Diagnostics", ToolTip = "Toggle diagnostics panel", Command = ToggleDiagnosticsPanelCommand },
        // Grip-only resize toggle - a diagonal resize double-arrow.
        new WindowCommand { IconData = "M2,2 L12,12 M12,12 L12,8 M12,12 L8,12 M2,2 L2,6 M2,2 L6,2", Label = "Resize mode", ToolTip = "Toggle grip-only resize", Command = ToggleGripResizeCommand },
    };
}