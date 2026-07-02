using System.Threading.Tasks;
using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

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
    }

    [Bindable, Affects(nameof(MenuButtonText))] private bool _isMenuVisible = true;
    public string MenuButtonText => IsMenuVisible ? "Hide menu" : "Show menu";

    [Command] private void ToggleMenu() => IsMenuVisible = !IsMenuVisible;

    [Bindable] private string _status = "F-15C Eagle";

    [Command] private Task LoadF15() => Load("Models/F15C/F-15C_Eagle.dae", "F-15C Eagle");

    [Command] private Task LoadMonkey() => Load("Models/monkey/monkey.dae", "Monkey");

    [Command] private Task LoadSwordman() => Load("Models/swordman.dae", "Swordman");

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
