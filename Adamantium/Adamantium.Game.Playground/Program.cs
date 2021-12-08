using Adamantium.Engine.Graphics;

namespace Adamantium.Game.Playground
{
    using Game = Adamantium.Engine.Game;

    class Program
    {
        static void Main(string[] args)
        {
            var game = new AdamantiumGame(GameMode.Standalone);
            var wnd = game.CreateWindow();
            wnd.Show();
            game.Run();
        }
    }
}
