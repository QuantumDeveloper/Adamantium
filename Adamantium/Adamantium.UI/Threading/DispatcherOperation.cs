using System;
using System.Threading.Tasks;

namespace Adamantium.UI.Threading
{
    public class DispatcherOperation : IDispatcherOperation
    {
        public DispatcherOperation(Action action, DispatcherPriority priority)
        {
            Action = action;
            Priority = priority;
            Task = new TaskCompletionSource<object>().Task;
        }
        
        public Action Action { get; }
        public DispatcherPriority Priority { get; }
        
        private readonly TaskCompletionSource<object> taskCompletionSource;
        public void Run()
        {
            try
            {
                Action();
                taskCompletionSource.SetResult(null);
            }
            catch (Exception e)
            {
                taskCompletionSource.SetException(e);
            }
        }

        public Task Task { get; }

        
    }
}