using System;
using System.Threading.Tasks;
using Adamantium.ECS.Components;
using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Core.Dispatcher;

namespace Adamantium.UI.Sandbox.ViewModels;

/// <summary>Game tab: a RenderTargetPanel runs the 3D game as the backdrop, with a simple in-game-style overlay menu on
/// top. The menu's show/hide is fully view-model driven; the "Load …" commands swap the model shown in the scene at
/// runtime via the game bridge (<see cref="AttachGame"/>, populated by GameHostBehavior once the game is live).</summary>
[ViewModel]
public partial class GameViewModel : TabPageViewModel
{
    public GameViewModel() : base("Game") { }

    private AdamantiumGame _game;

    /// <summary>Called by GameHostBehavior once the hosted game exists, so the menu's load commands can reach it.</summary>
    internal void AttachGame(AdamantiumGame game)
    {
        _game = game;
        Status = "Game ready";

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

    /// <summary>How fast the camera travels through the world, in units per second. The engine's own default of 1 is a
    /// crawl at this scene's scale, so the demo asks for something a person can fly with.</summary>
    [Bindable] private double _cameraSpeed = 50;

    // What we last handed the camera. The game doubles and halves the velocity on its own keys (numpad + / -), so
    // anything else found there came from the keyboard and the box should follow it rather than fight it.
    private double _handed = 50;

    // Where the camera stood the first time we saw it. Remembered rather than assumed: whatever the engine placed it
    // at IS home, and that is the one place a lost person can be put back.
    private Vector3? _home;
    private QuaternionF _homeRotation;

    private void OnPulse(object sender, EventArgs e)
    {
        if (_game == null) return;

        Fps = _game.RenderFps;
        FrameCostMs = _game.DrawTimeMs;

        if (_game.CameraManager?.UserControlledCamera is not { } camera) return;

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
        if (_home is not { } home || _game?.CameraManager?.UserControlledCamera is not { } camera) return;

        // Flown, not teleported - and the rotation first, because both share the field that records where the travel
        // started. The orientation gizmo mirrors the camera, so it swings back along with it.
        camera.RotateAroundSelectedObject(_homeRotation, ResetMilliseconds);
        camera.MoveTo(home, ResetMilliseconds);
    }

    private const int ResetMilliseconds = 500;

    partial void OnCameraSpeedChanged(double value)
    {
        _handed = value;

        if (_game?.CameraManager?.UserControlledCamera is { } camera) camera.Velocity = value;
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
        await _game.LoadAndAddModel(path);
        Status = name;
    }
}
