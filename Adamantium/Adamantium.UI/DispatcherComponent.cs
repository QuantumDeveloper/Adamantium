using System.Threading;
using Adamantium.UI.Controls;

namespace Adamantium.UI
{
    public class DispatcherComponent : AdamantiumComponent
    {
        private Thread initialThread;
        
        protected DispatcherComponent()
        {
            initialThread = Thread.CurrentThread;
            Dispatcher = Dispatcher.CurrentDispatcher;
        }

        public Dispatcher Dispatcher { get; set; }
        
        public void VerifyAccess()
        {

        }

        public void CheckAccess()
        {

        }
    }
}