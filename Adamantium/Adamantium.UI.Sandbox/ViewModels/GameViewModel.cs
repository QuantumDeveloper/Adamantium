using System;
using System.Threading.Tasks;
using Adamantium.ECS.Components;
using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Core.Dispatcher;

namespace Adamantium.UI.Sandbox.ViewModels;

/// <summary>Game tab: the 3D game behind an overlay menu. The load commands reach the game through
/// <see cref="AttachGame"/>, which GameHostBehavior calls once it is live.</summary>
[ViewModel]
public partial class GameViewModel : TabPageViewModel
{
    public GameViewModel() : base("Game") { }

    private AdamantiumGame _game;

    /// <summary>Called by GameHostBehavior once the hosted game exists, so the menu's load commands can reach it.</summary>
    internal void AttachGame(AdamantiumGame game)
    {
        _game = game;
        _game.UseTool(Tool);
        Status = "Game ready";

        // A rebuilt view brings a new game with a new camera: its home and speed are taken afresh on the next pulse.
        _home = null;

        // Once the game is there, the readout has something to read. On the UI thread, so nothing touched here ever
        // crosses over from the game loop.
        _pulse ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.25) };
        _pulse.Tick -= OnPulse;
        _pulse.Tick += OnPulse;
        _pulse.Start();
    }

    private DispatcherTimer _pulse;

    /// <summary>What the GAME's own rendering could sustain, and what one of its frames costs. Not the rate the panel
    /// ticks it at - hosted, that would only ever report the interface.</summary>
    [Bindable] private float _fps;

    [Bindable] private double _frameCostMs;

    /// <summary>How fast the camera travels through the world, in meters per second.</summary>
    [Bindable] private double _cameraSpeed = 0.5;

    // What we last handed the camera. The game doubles and halves the velocity on its own keys (numpad + / -), so
    // anything else found there came from the keyboard and the box should follow it rather than fight it.
    private double _handed = 0.5;

    // Where the camera stood the first time we saw it. Remembered rather than assumed: whatever the engine placed it
    // at IS home, and that is the one place a lost person can be put back.
    private Vector3? _home;
    private QuaternionF _homeRotation;

    private void OnPulse(object sender, EventArgs e)
    {
        if (_game == null) return;

        Fps = _game.RenderFps;
        FrameCostMs = _game.DrawTimeMs;

        if (_game.MainOutput?.Camera is not { } camera) return;

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
        if (_home is not { } home || _game?.MainOutput?.Camera is not { } camera) return;

        // Flown, not teleported - and the rotation first, because both share the field that records where the travel
        // started. The orientation gizmo mirrors the camera, so it swings back along with it.
        camera.RotateAroundSelectedObject(_homeRotation, ResetMilliseconds);
        camera.MoveTo(home, ResetMilliseconds);
    }

    private const int ResetMilliseconds = 500;

    partial void OnCameraSpeedChanged(double value)
    {
        _handed = value;

        if (_game?.MainOutput?.Camera is { } camera) camera.Velocity = value;
    }

    /// <summary>The tool the mouse works with in the game.</summary>
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
        _game?.UseTool(value);
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
        if (_game == null)
        {
            Status = "Game not ready yet";
            return;
        }

        Status = $"Loading {name}…";
        try
        {
            await _game.LoadAndAddModel(path);
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
