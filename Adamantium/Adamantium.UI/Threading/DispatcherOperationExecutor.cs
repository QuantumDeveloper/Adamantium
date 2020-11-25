using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Adamantium.UI.Threading
{
    internal class DispatcherOperationExecutor
    {
        private Queue<IDispatcherOperation>[] operationQueue = new Queue<IDispatcherOperation>[(int)DispatcherPriority.MaxValue + 1];

        public DispatcherOperationExecutor()
        {
            
        }
        
        public Task InvokeAsync(Action action, DispatcherPriority priority)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            
            var operation = new DispatcherOperation(action, priority);
            AddOperation(operation);
            return operation.Task;
        }

        public void ExecuteOperations()
        {
            
        }

        private void AddOperation(IDispatcherOperation operation)
        {
            operationQueue[(int)operation.Priority].Enqueue(operation);
        }
    }
}