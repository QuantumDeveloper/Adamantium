using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Platforms;

namespace Adamantium.Game.Core
{
    public class UniversePlatformWindows : UniversePlatformDesktop
    {
        private IApplicationPlatform applicationPlatform;

        public UniversePlatformWindows(IUniverse universe, IDependencyResolver resolver) : base(universe)
        {
            applicationPlatform = resolver.Resolve<IApplicationPlatform>();
        }

        public override void Run(CancellationToken token)
        {
            applicationPlatform.Run(token);
        }
    }
}