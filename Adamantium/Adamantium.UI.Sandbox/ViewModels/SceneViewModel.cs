using System;
using System.Threading.Tasks;
using Adamantium.ECS.Components;
using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Core.Dispatcher;

namespace Adamantium.UI.Sandbox.ViewModels;

/// <summary>Scene tab: the 3D universe behind an overlay menu. The load commands reach the universe through
/// <see cref="AttachUniverse"/>, which DemoUniverseBehavior calls once it is live.</summary>
[ViewModel]
public partial class SceneViewModel : TabPageViewModel
{
    public SceneViewModel() : base("Scene") { }

    private DemoUniverse _universe;

    /// <summary>Called by DemoUniverseBehavior once the hosted universe exists, so the menu's load commands can reach it.</summary>
    internal void AttachUniverse(DemoUniverse universe)
    {
        _universe = universe;
        _universe.UseTool(Tool);
        Status = "Universe ready";

        // A rebuilt view brings a new universe with a new camera: its home and speed are taken afresh on the next pulse.
        _home = null;

        // Once the universe is there, the readout has something to read. On the UI thread, so nothing touched here ever
        // crosses over from the universe loop.
        _pulse ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.25) };
        _pulse.Tick -= OnPulse;
        _pulse.Tick += OnPulse;
        _pulse.Start();
    }

    private DispatcherTimer _pulse;

    /// <summary>What the UNIVERSE's own rendering could sustain, and what one of its frames costs. Not the rate the panel
    /// ticks it at - hosted, that would only ever report the interface.</summary>
    [Bindable] private float _fps;

    [Bindable] private double _frameCostMs;

    /// <summary>How fast the camera travels through the world, in meters per second.</summary>
    [Bindable] private double _cameraSpeed = 0.5;

    // What we last handed the camera. The universe doubles and halves the velocity on its own keys (numpad + / -), so
    // anything else found there came from the keyboard and the box should follow it rather than fight it.
    private double _handed = 0.5;

    // Where the camera stood the first time we saw it. Remembered rather than assumed: whatever the engine placed it
    // at IS home, and that is the one place a lost person can be put back.
    private Vector3? _home;
    private QuaternionF _homeRotation;

    private void OnPulse(object sender, EventArgs e)
    {
        if (_universe == null) return;

        Fps = _universe.RenderFps;
        FrameCostMs = _universe.DrawTimeMs;

        if (_universe.MainOutput?.Camera is not { } camera) return;

        if (_home == null)
        {
            _home = camera.Owner.Transform.Position;
            _homeRotation = camera.Rotation;

            // The camera exists now, so the speed the tab is showing becomes the speed it actually travels at.
            camera.Velocity = CameraSpeed;
            _handed = CameraSpeed;
            return;
        }

        if (Math.Abs(camera.Velocity - _handed) < 1e-9) return;

        _handed = camera.Velocity;
        CameraSpeed = camera.Velocity;
    }

    /// <summary>Puts the camera back where it started - the way out of being lost in a world with no landmarks.</summary>
    [Command] private void ResetCamera()
    {
        if (_home is not { } home || _universe?.MainOutput?.Camera is not { } camera) return;

        // Flown, not teleported - and the rotation first, because both share the field that records where the travel
        // started. The orientation gizmo mirrors the camera, so it swings back along with it.
        camera.RotateAroundSelectedObject(_homeRotation, ResetMilliseconds);
        camera.MoveTo(home, ResetMilliseconds);
    }

    private const int ResetMilliseconds = 500;

    partial void OnCameraSpeedChanged(double value)
    {
        _handed = value;

        if (_universe?.MainOutput?.Camera is { } camera) camera.Velocity = value;
    }

    /// <summary>The tool the mouse works with in the universe.</summary>
    [Bindable, Affects(nameof(IsSelecting), nameof(IsMoving), nameof(IsRotating), nameof(IsScaling), nameof(IsMovingPivot))]
    private EditTool _tool = EditTool.Select;

    public bool IsSelecting
    {
        get => Tool == EditTool.Select;
        set => Choose(EditTool.Select, value);
    }

    public bool IsMoving
    {
        get => Tool == EditTool.Move;
        set => Choose(EditTool.Move, value);
    }

    public bool IsRotating
    {
        get => Tool == EditTool.Rotate;
        set => Choose(EditTool.Rotate, value);
    }

    public bool IsScaling
    {
        get => Tool == EditTool.Scale;
        set => Choose(EditTool.Scale, value);
    }

    public bool IsMovingPivot
    {
        get => Tool == EditTool.Pivot;
        set => Choose(EditTool.Pivot, value);
    }

    private void Choose(EditTool tool, bool chosen)
    {
        if (chosen)
        {
            Tool = tool;
        }
    }

    partial void OnToolChanged(EditTool value)
    {
        _universe?.UseTool(value);
    }

    [Bindable, Affects(nameof(MenuButtonText), nameof(MouseLookEnabled))] private bool _isMenuVisible = true;
    public string MenuButtonText => IsMenuVisible ? "Hide menu" : "Show menu";

    /// <summary>Mouse-look is allowed only while the menu is HIDDEN - so a click on the panel with the menu up doesn't
    /// grab and hide the cursor. Bound to the panel's IsMouseLookEnabled.</summary>
    public bool MouseLookEnabled => !IsMenuVisible;

    [Command] private void ToggleMenu() => IsMenuVisible = !IsMenuVisible;

    [Bindable] private string _status = "F-15C Eagle";

    [Command] private Task LoadF15() => Load("Models/F15C/F-15C_Eagle.dae", "F-15C Eagle");

    [Command] private Task LoadMonkey() => Load("Models/monkey/monkey.dae", "Monkey");

    private async Task Load(string path, string name)
    {
        if (_universe == null)
        {
            Status = "Universe not ready yet";
            return;
        }

        Status = $"Loading {name}…";
        try
        {
            await _universe.LoadAndAddModel(path);
            Status = name;
        }
        catch (Exception exception)
        {
            // Otherwise the line would sit at "Loading …" while the cause went into an unobserved Task
            Status = $"{name}: failed to load — {exception.GetBaseException().Message}";
            Console.WriteLine(exception);
        }
    }
}
