using System;
using Adamantium.UI.Input;

namespace Adamantium.UI.OSX
{
    public class OSXApplication : Application
    {
        internal override MouseDevice MouseDevice { get; }
        internal override KeyboardDevice KeyboardDevice { get; }
        
        public override void Run()
        {
            throw new NotImplementedException();
        }
    }
}