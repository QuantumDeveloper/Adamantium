using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.Game.Core;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;

namespace Adamantium.Game;

public interface IGameService
{
    public IReadOnlyList<IGame> Games { get; }

    public T CreateGame<T>(string name, IWindow wnd, EntityService service, params object[] args) where T : IGame;

    public bool RemoveGame(IGame game);

    public void RunGames(IRenderService renderService, AppTime time);

    public void CopyOutput(IGraphicsDevice graphicsDevice);

    public event Action<IGame> OnGameAdded;
}