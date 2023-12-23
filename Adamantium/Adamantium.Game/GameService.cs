using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Game.Core;
using Adamantium.UI.Controls;

namespace Adamantium.Game;

public class GameService : IGameService
{
    private readonly object _locker = new object();
    
    private List<GameKey> _games;

    public GameService()
    {
        _games = new List<GameKey>();
    }

    public IReadOnlyList<IGame> Games
    {
        get
        {
            lock (_locker)
            {
                return _games.Select(x=>x.Game).ToList();
            }
        }  
    } 

    public T CreateGame<T>(string name, IWindow wnd, EntityService service, params object[] args) where T : IGame
    {
        var game = (T)Activator.CreateInstance(typeof(T), args);
        game.InitializeGame();
        var key = new GameKey(name, wnd, service, game);
        lock (_locker)
        {
            _games.Add(key);
        }

        OnGameAdded?.Invoke(game);
        return game;
    }

    public bool RemoveGame(IGame game)
    {
        lock (_locker)
        {
            var result = _games.FirstOrDefault(x => x.Game == game);
            return _games.Remove(result);
        }
    }

    public void RunGames(IRenderService renderService, AppTime time)
    {
        lock (_locker)
        {
            Parallel.ForEach(_games, (item) =>
            {
                if (item.Service != renderService)  return;
                
                item.Game.RunOnce(time);
            });
            foreach (var game in _games)
            {
                ((Game)game.Game).Submit();
            }
        }
    }

    public void CopyOutput(GraphicsDevice graphicsDevice)
    {
        foreach (var game in Games)
        {
            foreach (var gameOutput in game.Outputs)
            {
                gameOutput.CopyOutput(graphicsDevice);
            }
        }
    }

    public event Action<IGame> OnGameAdded;
    
    private class GameKey
    {
        public GameKey(string name, IWindow window, EntityService service, IGame game)
        {
            Name = name;
            Window = window;
            Service = service;
            Game = game;
        }
        
        public string Name { get; }
        
        public IWindow Window { get; }
        
        public EntityService Service { get; }
        
        public IGame Game { get; }

        public override bool Equals(object obj)
        {
            if (obj is GameKey key)
            {
                return this == key;
            }

            return false;
        }
        
        public static bool operator == (GameKey key1, GameKey key2)
        {
            if (key1 == null || key2 == null) return false;

            return key1 == key2;
        }

        public static bool operator !=(GameKey key1, GameKey key2)
        {
            return !(key1 == key2);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name.GetHashCode(), Window.GetHashCode(), Service.GetHashCode(), Game.GetHashCode());
        }
    }
}