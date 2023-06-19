using System.Collections.Generic;
using Adamantium.Game.Core;

namespace Adamantium.Game;

public interface IGameService
{
    public IReadOnlyList<IGame> Games { get; }

    public T CreateGame<T>(string name, params object[] args) where T : IGame;

    public bool RemoveGame(IGame game);
}