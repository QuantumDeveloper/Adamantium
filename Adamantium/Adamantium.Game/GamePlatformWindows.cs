using System.Threading;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Threading;

namespace Adamantium.Game
{
    public class GamePlatformWindows : GamePlatformDesktop
    {
        private IApplicationPlatform applicationPlatform;

        static GamePlatformWindows()
        {
            WindowsPlatform.Initialize();
        }
        
        public GamePlatformWindows(GameBase gameBase) : base(gameBase)
        {
            applicationPlatform = AdamantiumDependencyResolver.Current.Resolve<IApplicationPlatform>();
        }

        public override void Run(CancellationToken token)
        {
            applicationPlatform.Run(token);
        }
    }
}