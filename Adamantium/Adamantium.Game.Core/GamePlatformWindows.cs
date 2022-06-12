using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Threading;

namespace Adamantium.Game.Core
{
    public class GamePlatformWindows : GamePlatformDesktop
    {
        private IApplicationPlatform applicationPlatform;

        public GamePlatformWindows(IGame gameBase, IDependencyResolver resolver) : base(gameBase)
        {
            applicationPlatform = resolver.Resolve<IApplicationPlatform>();
        }

        public override void Run(CancellationToken token)
        {
            applicationPlatform.Run(token);
        }
    }
}