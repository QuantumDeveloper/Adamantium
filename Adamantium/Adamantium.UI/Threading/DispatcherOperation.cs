using System;
using System.Threading.Tasks;

namespace Adamantium.UI.Threading
{
    public class DispatcherOperation : IDispatcherOperation
    {
        private readonly TaskCompletionSource<object> taskCompletionSource;
        public DispatcherOperation(Action action, DispatcherPriority priority)
        {
            Action = action;
            Priority = priority;
            taskCompletionSource = new TaskCompletionSource<object>();
        }
        
        public Action Action { get; }
        public DispatcherPriority Priority { get; }
        
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

        public Task Task => taskCompletionSource.Task;


    }
}