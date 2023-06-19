using System;
using System.Collections.Generic;
using Adamantium.Game.Core;

namespace Adamantium.Game;

public class GameService : IGameService
{
    private readonly object _locker = new object();
    
    private List<IGame> _games;
    private Dictionary<string, IGame> _gamesByName;

    public GameService()
    {
        _games = new List<IGame>();
    }

    public IReadOnlyList<IGame> Games => _games.AsReadOnly();
    
    public T CreateGame<T>(string name, params object[] args) where T : IGame
    {
        var game = (T)Activator.CreateInstance(typeof(T), args);
        lock (_locker)
        {
            _games.Add(game);
        }
        return game;
    }

    public bool RemoveGame(IGame game)
    {
        lock (_locker)
        {
            return _games.Remove(game);
        }
    }
}