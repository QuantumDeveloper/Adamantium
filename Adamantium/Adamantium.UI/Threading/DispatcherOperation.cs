using System;
using System.Threading.Tasks;

namespace Adamantium.UI.Threading
{
    public class DispatcherOperation : IDispatcherOperation
    {
        private readonly TaskCompletionSource<object> taskCompletionSource;
        public DispatcherOperation(Delegate action, DispatcherPriority priority)
        {
            Action = action;
            Priority = priority;
            taskCompletionSource = new TaskCompletionSource<object>();
        }
        
        public DispatcherOperation(Delegate method, object args, int numArgs)
        {
            Action = method;
            Priority = DispatcherPriority.Normal;
            arguments = args;
            this.numArgs = numArgs;
            taskCompletionSource = new TaskCompletionSource<object>();
        }

        private readonly object arguments;
        private readonly int numArgs;
        
        public Delegate Action { get; }
        public DispatcherPriority Priority { get; }
        
        public void Run()
        {
            try
            {
                if (numArgs == 0)
                {
                   var action = Action as Action;
                   action?.Invoke();
                }
                else if (numArgs == 1)
                {
                    Action?.DynamicInvoke(arguments);
                }
                else
                {
                    Action?.DynamicInvoke((object[])arguments);
                }
                
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