using System.Collections.Generic;
using Adamantium.Game.Core;
using Adamantium.UI.Controls;

namespace Adamantium.Game.Playground;

public class AdamantiumGameApplication : GameApplication
{
    private List<GameOutput> outputs;
    public AdamantiumGameApplication()
    {
        outputs = new List<GameOutput>();
    }
        
    protected override Game OnCreateGameInstance()
    {
        return new AdamantiumGame();
    }
    
    protected override void OnWindowCreated(IWindow wnd)
    {
        outputs.Add(GameOutput.New(new GameContext(wnd)));
    }

    protected override void OnGameInitialized()
    {
        foreach (var window in outputs)
        {
            Game.AddOutput(window);
        }
    }
}