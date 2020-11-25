using System.Threading;

namespace Adamantium.UI.Threading
{
    public interface IApplicationPlatform
    {
        void Run(CancellationToken token);
        
        bool IsOnUIThread { get; }
        
    }
}