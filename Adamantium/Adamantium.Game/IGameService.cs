using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Game.Core;
using Adamantium.UI.Controls;

namespace Adamantium.Game;

public interface IGameService
{
    public IReadOnlyList<IGame> Games { get; }

    public T CreateGame<T>(string name, IWindow wnd, EntityService service, params object[] args) where T : IGame;

    public bool RemoveGame(IGame game);

    public void RunGames(IRenderService renderService, AppTime time);

    public void WaitForGames();

    public void CopyOutput(GraphicsDevice graphicsDevice);

    public event Action<IGame> OnGameAdded;
}