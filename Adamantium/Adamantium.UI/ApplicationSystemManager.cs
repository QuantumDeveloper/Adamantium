using Adamantium.Engine.Core;

namespace Adamantium.UI
{
    public class ApplicationSystemManager : SystemManager
    {
        public ApplicationSystemManager(IService runningService)
            : base(runningService)
        {
        }
    }
}