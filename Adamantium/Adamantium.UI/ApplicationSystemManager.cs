using Adamantium.Engine.Core;

namespace Adamantium.UI
{
    public class ApplicationSystemManager : SystemManager
    {
        public ApplicationSystemManager(IRunningService runningService)
            : base(runningService)
        {
        }
    }
}